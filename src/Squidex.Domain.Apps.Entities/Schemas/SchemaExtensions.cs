﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using Squidex.Domain.Apps.Core.Schemas;
using Squidex.Infrastructure;

namespace Squidex.Domain.Apps.Entities.Schemas
{
    public static class SchemaExtensions
    {
        public static NamedId<Guid> NamedId(this ISchemaEntity schema)
        {
            return new NamedId<Guid>(schema.Id, schema.Name);
        }

        public static string EscapePartition(this string value)
        {
            return value.Replace('-', '_');
        }

        public static string TypeName(this IField field)
        {
            return field.Name.ToPascalCase();
        }

        public static string TypeName(this ISchemaEntity schema)
        {
            return schema.SchemaDef.Name.ToPascalCase();
        }

        public static string DisplayName(this IField field)
        {
            return field.RawProperties.Label.WithFallback(field.TypeName());
        }

        public static string DisplayName(this ISchemaEntity schema)
        {
            return schema.SchemaDef.Properties.Label.WithFallback(schema.TypeName());
        }

        public static string TypeName(this Schema schema)
        {
            return schema.Name.ToPascalCase();
        }

        public static string DisplayName(this Schema schema)
        {
            return schema.Properties.Label.WithFallback(schema.TypeName());
        }
    }
}
