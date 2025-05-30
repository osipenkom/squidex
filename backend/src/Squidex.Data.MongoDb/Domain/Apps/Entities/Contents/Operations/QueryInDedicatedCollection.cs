﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using MongoDB.Driver;
using Squidex.Domain.Apps.Core.Apps;
using Squidex.Domain.Apps.Core.Contents;
using Squidex.Domain.Apps.Core.Schemas;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Queries;
using Squidex.Infrastructure.States;

namespace Squidex.Domain.Apps.Entities.Contents.Operations;

internal sealed class QueryInDedicatedCollection(IMongoClient mongoClient, string prefixDatabase, string prefixCollection) : MongoBase<MongoContentEntity>
{
    private readonly CollectionProvider collections = new CollectionProvider(mongoClient, prefixDatabase, prefixCollection);

    public Task<IMongoCollection<MongoContentEntity>> GetCollectionAsync(DomainId appId, DomainId schemaId)
    {
        return collections.GetCollectionAsync(appId, schemaId);
    }

    public async Task<IReadOnlyList<ContentIdStatus>> QueryIdsAsync(App app, Schema schema, FilterNode<ClrValue> filterNode,
        CancellationToken ct)
    {
        // We need to translate the filter names to the document field names in MongoDB.
        var adjustedFilter = filterNode.AdjustToModel(app.Id);

        var filter = BuildFilter(adjustedFilter);

        var contentCollection = await GetCollectionAsync(schema.AppId.Id, schema.Id);
        var contentEntities = await contentCollection.FindStatusAsync(filter, ct);
        var contentResults = contentEntities.Select(x => new ContentIdStatus(x.IndexedSchemaId, x.Id, x.Status)).ToList();

        return contentResults;
    }

    public async Task<IResultList<Content>> QueryAsync(Schema schema, Q q,
        CancellationToken ct)
    {
        // We need to translate the query names to the document field names in MongoDB.
        var query = q.Query.AdjustToContentModel(schema.AppId.Id);

        var filter = CreateFilter(query, q.Reference, q.CreatedBy);

        var contentCollection = await GetCollectionAsync(schema.AppId.Id, schema.Id);
        var contentEntities = await contentCollection.QueryContentsAsync(filter, query, q, ct);
        var contentTotal = (long)contentEntities.Count;

        if (contentTotal >= query.Take || query.Skip > 0)
        {
            if (q.NoTotal || (q.NoSlowTotal && query.Filter != null))
            {
                contentTotal = -1;
            }
            else if (query.IsSatisfiedByIndex())
            {
                // It is faster to filter with sorting when there is an index, because it forces the index to be used.
                contentTotal = await contentCollection.Find(filter).QuerySort(query).CountDocumentsAsync(ct);
            }
            else
            {
                contentTotal = await contentCollection.Find(filter).CountDocumentsAsync(ct);
            }
        }

        return ResultList.Create<Content>(contentTotal, contentEntities);
    }

    public async Task UpsertAsync(SnapshotWriteJob<MongoContentEntity> job,
        CancellationToken ct = default)
    {
        var collection = await GetCollectionAsync(job.Value.AppId.Id, job.Value.SchemaId.Id);

        await collection.ReplaceOneAsync(Filter.Eq(x => x.DocumentId, job.Key), job.Value, UpsertReplace, ct);
    }

    public async Task UpsertVersionedAsync(IClientSessionHandle session, SnapshotWriteJob<MongoContentEntity> job,
        CancellationToken ct = default)
    {
        var collection = await GetCollectionAsync(job.Value.AppId.Id, job.Value.SchemaId.Id);

        await collection.UpsertVersionedAsync(session, job, Field.Of<MongoContentEntity>(x => nameof(x.Version)), ct);
    }

    public async Task RemoveAsync(MongoContentEntity value,
        CancellationToken ct = default)
    {
        var collection = await GetCollectionAsync(value.AppId.Id, value.SchemaId.Id);

        await collection.DeleteOneAsync(x => x.DocumentId == value.DocumentId, null, ct);
    }

    public async Task RemoveAsync(IClientSessionHandle session, MongoContentEntity value,
        CancellationToken ct = default)
    {
        var collection = await GetCollectionAsync(value.AppId.Id, value.SchemaId.Id);

        await collection.DeleteOneAsync(session, x => x.DocumentId == value.DocumentId, null, ct);
    }

    public async Task DropIndexAsync(DomainId appId, DomainId schemaId, string name,
        CancellationToken ct)
    {
        var collection = await GetCollectionAsync(appId, schemaId);

        await collection.Indexes.DropOneAsync(name, ct);
    }

    public async Task<List<IndexDefinition>> GetIndexesAsync(DomainId appId, DomainId schemaId,
        CancellationToken ct = default)
    {
        var result = new List<IndexDefinition>();

        var collection = await GetCollectionAsync(appId, schemaId);
        var colIndexes = await collection.Indexes.ListAsync(ct);

        foreach (var index in await colIndexes.ToListAsync(ct))
        {
            if (IndexParser.TryParse(index, "custom_", out var definition))
            {
                result.Add(definition);
            }
        }

        return result;
    }

    public async Task CreateIndexAsync(DomainId appId, DomainId schemaId, IndexDefinition index,
        CancellationToken ct)
    {
        var collection = await GetCollectionAsync(appId, schemaId);

        var definition = Index.Combine(
            index.Select(field =>
            {
                var path = Adapt.MapPath(field.Name).ToString();

                if (field.Order == SortOrder.Ascending)
                {
                    return Index.Ascending(path);
                }

                return Index.Descending(path);
            }));

        var name = $"custom_{index.ToName()}";

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<MongoContentEntity>(
                definition,
                new CreateIndexOptions
                {
                    Name = name,
                }),
            cancellationToken: ct);
    }

    private static FilterDefinition<MongoContentEntity> BuildFilter(FilterNode<ClrValue>? filter)
    {
        var filters = new List<FilterDefinition<MongoContentEntity>>
        {
            Filter.Exists(x => x.LastModified),
            Filter.Exists(x => x.Id),
        };

        if (filter?.HasField(Field.Of<MongoContentEntity>(x => nameof(x.IsDeleted))) != true)
        {
            filters.Add(Filter.Ne(x => x.IsDeleted, true));
        }

        if (filter != null)
        {
            filters.Add(filter.BuildFilter<MongoContentEntity>());
        }

        return Filter.And(filters);
    }

    private static FilterDefinition<MongoContentEntity> CreateFilter(ClrQuery? query,
        DomainId reference, RefToken? createdBy)
    {
        var filters = new List<FilterDefinition<MongoContentEntity>>
        {
            Filter.Gt(x => x.LastModified, default),
            Filter.Gt(x => x.Id, default),
        };

        if (query?.Filter?.HasField(Field.Of<MongoContentEntity>(x => nameof(x.IsDeleted))) != true)
        {
            filters.Add(Filter.Ne(x => x.IsDeleted, true));
        }

        if (query?.Filter != null)
        {
            filters.Add(query.Filter.BuildFilter<MongoContentEntity>());
        }

        if (reference != default)
        {
            filters.Add(Filter.AnyEq(x => x.ReferencedIds, reference));
        }

        if (createdBy != null)
        {
            filters.Add(Filter.Eq(x => x.CreatedBy, createdBy));
        }

        return Filter.And(filters);
    }
}
