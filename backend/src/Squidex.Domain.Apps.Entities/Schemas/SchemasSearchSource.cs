﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Domain.Apps.Core;
using Squidex.Domain.Apps.Core.Apps;
using Squidex.Domain.Apps.Core.Schemas;
using Squidex.Domain.Apps.Entities.Search;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Translations;
using Squidex.Shared;

namespace Squidex.Domain.Apps.Entities.Schemas;

public sealed class SchemasSearchSource(IAppProvider appProvider, IUrlGenerator urlGenerator) : ISearchSource
{
    public async Task<SearchResults> SearchAsync(string query, Context context,
        CancellationToken ct)
    {
        var result = new SearchResults();

        var schemas = await appProvider.GetSchemasAsync(context.App.Id, ct);

        if (schemas.Count <= 0)
        {
            return result;
        }

        var appId = context.App.NamedId();

        foreach (var schema in schemas)
        {
            var schemaId = schema.NamedId();

            var name = schema.DisplayName();

            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                AddSchemaUrl(result, appId, schemaId, name);

                if (schema.Type != SchemaType.Component && HasPermission(context, schemaId))
                {
                    AddContentsUrl(result, appId, schema, schemaId, name);
                }
            }
        }

        return result;
    }

    private void AddSchemaUrl(SearchResults result, NamedId<DomainId> appId, NamedId<DomainId> schemaId, string name)
    {
        var schemaUrl = urlGenerator.SchemaUI(appId, schemaId);

        result.Add(T.Get("search.schemaResult", new { name }), SearchResultType.Schema, schemaUrl);
    }

    private void AddContentsUrl(SearchResults result, NamedId<DomainId> appId, Schema schema, NamedId<DomainId> schemaId, string name)
    {
        if (schema.Type == SchemaType.Singleton)
        {
            var contentUrl = urlGenerator.ContentUI(appId, schemaId, schemaId.Id);

            result.Add(T.Get("search.contentResult", new { name }), SearchResultType.Content, contentUrl, name);
        }
        else
        {
            var contentUrl = urlGenerator.ContentsUI(appId, schemaId);

            result.Add(T.Get("search.contentsResult", new { name }), SearchResultType.Content, contentUrl, name);
        }
    }

    private static bool HasPermission(Context context, NamedId<DomainId> schemaId)
    {
        return context.Allows(PermissionIds.AppContentsReadOwn, schemaId.Name);
    }
}
