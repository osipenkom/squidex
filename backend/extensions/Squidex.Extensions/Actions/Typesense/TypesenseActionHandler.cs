﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Text;
using System.Text.Json.Serialization;
using Squidex.Domain.Apps.Core.HandleRules;
using Squidex.Domain.Apps.Core.Rules.EnrichedEvents;
using Squidex.Domain.Apps.Core.Scripting;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Json;

#pragma warning disable IDE0059 // Value assigned to symbol is never used
#pragma warning disable MA0048 // File name must match type name

namespace Squidex.Extensions.Actions.Typesense;

public sealed class TypesenseActionHandler(RuleEventFormatter formatter, IHttpClientFactory httpClientFactory, IScriptEngine scriptEngine, IJsonSerializer serializer) : RuleActionHandler<TypesenseAction, TypesenseJob>(formatter)
{
    protected override async Task<(string Description, TypesenseJob Data)> CreateJobAsync(EnrichedEvent @event, TypesenseAction action)
    {
        var delete = @event.ShouldDelete(scriptEngine, action.Delete);

        string contentId;

        if (@event is IEnrichedEntityEvent enrichedEntityEvent)
        {
            contentId = enrichedEntityEvent.Id.ToString();
        }
        else
        {
            contentId = DomainId.NewGuid().ToString();
        }

        var indexName = await FormatAsync(action.IndexName, @event);
        var ruleText = string.Empty;
        var ruleJob = new TypesenseJob
        {
            ServerUrl = $"{action.Host.ToString().TrimEnd('/')}/collections/{indexName}/documents",
            ServerKey = action.ApiKey,
            ContentId = contentId,
        };

        if (delete)
        {
            ruleText = $"Delete entry index: {indexName}";
        }
        else
        {
            ruleText = $"Upsert to index: {indexName}";

            TypesenseContent content;
            try
            {
                string? jsonString;

                if (!string.IsNullOrEmpty(action.Document))
                {
                    jsonString = await FormatAsync(action.Document, @event);
                    jsonString = jsonString?.Trim();
                }
                else
                {
                    jsonString = ToJson(@event);
                }

                content = serializer.Deserialize<TypesenseContent>(jsonString!);
            }
            catch (Exception ex)
            {
                content = new TypesenseContent
                {
                    More = new Dictionary<string, object>
                    {
                        ["error"] = $"Invalid JSON: {ex.Message}",
                    },
                };
            }

            content.Id = contentId;

            ruleJob.Content = serializer.Serialize(content, true);
        }

        return (ruleText, ruleJob);
    }

    protected override async Task<Result> ExecuteJobAsync(TypesenseJob job,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(job.ServerUrl))
        {
            return Result.Ignored();
        }

        var httpClient = httpClientFactory.CreateClient("TypesenseAction");

        HttpRequestMessage request;

        if (job.Content != null)
        {
            request = new HttpRequestMessage(HttpMethod.Post, $"{job.ServerUrl}?action=upsert")
            {
                Content = new StringContent(job.Content, Encoding.UTF8, "application/json"),
            };
        }
        else
        {
            request = new HttpRequestMessage(HttpMethod.Delete, $"{job.ServerUrl}/{job.ContentId}");
        }

        request.Headers.TryAddWithoutValidation("X-Typesense-Api-Key", job.ServerKey);

        return await httpClient.OneWayRequestAsync(request, job.Content, ct);
    }
}

public sealed class TypesenseContent
{
    public string Id { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> More { get; set; } = [];
}

public sealed class TypesenseJob
{
    public string ServerUrl { get; set; }

    public string ServerKey { get; set; }

    public string Content { get; set; }

    public string ContentId { get; set; }
}
