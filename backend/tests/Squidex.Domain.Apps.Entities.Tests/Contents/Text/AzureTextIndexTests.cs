﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

#pragma warning disable SA1300 // Element should begin with upper-case letter

namespace Squidex.Domain.Apps.Entities.Contents.Text;

[Trait("Category", "Dependencies")]
public class AzureTextIndexTests(AzureTextIndexFixture fixture) : TextIndexerTestsBase, IClassFixture<AzureTextIndexFixture>
{
    public override bool SupportsGeo => true;

    public override int WaitAfterUpdate => 2000;

    public AzureTextIndexFixture _ { get; } = fixture;

    public override Task<ITextIndex> CreateSutAsync()
    {
        return Task.FromResult<ITextIndex>(_.Index);
    }

    [Fact]
    public async Task Should_retrieve_english_stopword_only_for_german_query()
    {
        await CreateTextAsync(ids1[0], "de", "and und");
        await CreateTextAsync(ids2[0], "en", "and und");

        await SearchText(expected: ids2, text: "und");
    }

    [Fact]
    public async Task Should_retrieve_german_stopword_only_for_english_query()
    {
        await CreateTextAsync(ids1[0], "de", "and und");
        await CreateTextAsync(ids2[0], "en", "and und");

        await SearchText(expected: ids1, text: "and");
    }

    [Fact]
    public async Task Should_index_cjk_content_and_retrieve()
    {
        await CreateTextAsync(ids1[0], "zh", "東京大学");

        await SearchText(expected: ids1, text: "東京");
    }
}
