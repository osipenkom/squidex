﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Domain.Apps.Core.Contents;
using Squidex.Domain.Apps.Core.TestHelpers;
using Squidex.Infrastructure.Json.Objects;

namespace Squidex.Domain.Apps.Core.Model.Contents;

public class ContentFieldDataTests
{
    [Fact]
    public void Should_serialize_and_deserialize()
    {
        var fieldData =
            new ContentFieldData()
                .AddInvariant(12);

        var serialized = fieldData.SerializeAndDeserializeAsJson();

        Assert.Equal(fieldData, serialized);
    }

    [Fact]
    public void Should_intern_invariant_key()
    {
        var fieldData =
            new ContentFieldData()
                .AddInvariant(12);

        var serialized = fieldData.SerializeAndDeserializeAsJson();

        Assert.NotNull(string.IsInterned(serialized.Keys.First()));
    }

    [Fact]
    public void Should_intern_known_language()
    {
        var fieldData =
            new ContentFieldData()
                .AddLocalized("en", 12);

        var serialized = fieldData.SerializeAndDeserializeAsJson();

        Assert.NotNull(string.IsInterned(serialized.Keys.First()));
    }

    [Fact]
    public void Should_not_intern_unknown_key()
    {
        var fieldData =
            new ContentFieldData()
                .AddLocalized(Guid.NewGuid().ToString(), 12);

        var serialized = fieldData.SerializeAndDeserializeAsJson();

        Assert.Null(string.IsInterned(serialized.Keys.First()));
    }

    [Fact]
    public void Should_clone_value_and_also_children()
    {
        var source = new ContentFieldData
        {
            ["en"] = new JsonArray(),
            ["de"] = new JsonArray(),
        };

        var clone = source.Clone();

        Assert.NotSame(source, clone);

        foreach (var (key, value) in clone)
        {
            Assert.NotSame(value.Value, source[key].Value);
        }
    }
}
