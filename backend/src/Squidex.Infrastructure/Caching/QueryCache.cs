﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Caching.Memory;

namespace Squidex.Infrastructure.Caching;

public class QueryCache<TKey, T>(IMemoryCache? cacheStore = null, string? cacheKeyPrefix = null) : IQueryCache<TKey, T> where TKey : notnull
{
    public void Set(TKey key, T item, TimeSpan cacheDuration)
    {
        if (cacheStore == null)
        {
            return;
        }

        cacheStore.Set((cacheKeyPrefix, key), item, cacheDuration);
    }

    public bool TryGet(TKey key, out T result)
    {
        result = default!;

        if (cacheStore == null)
        {
            return false;
        }

        if (cacheStore.TryGetValue((cacheKeyPrefix, key), out var item) && item is T typed)
        {
            result = typed;
            return true;
        }

        return false;
    }
}
