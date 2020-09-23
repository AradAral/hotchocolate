using HotChocolate.Execution.Configuration;
using HotChocolate.Types.Spatial;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace HotChocolate
{
    public static class SchemaBuilderExtensions
    {
        public static ISchemaBuilder AddSpatialTypes(this ISchemaBuilder builder)
        {
            return builder
                .AddType<GeoJsonInterface>()
                .AddType<GeoJsonGeometryType>()
                .AddType<GeoJsonPointInput>()
                .AddType<GeoJsonMultiPointInput>()
                .AddType<GeoJsonLineStringInput>()
                .AddType<GeoJsonMultiLineStringInput>()
                .AddType<GeoJsonPolygonInput>()
                .AddType<GeoJsonMultiPolygonInput>()
                .AddType<GeoJsonPointType>()
                .AddType<GeoJsonMultiPointType>()
                .AddType<GeoJsonLineStringType>()
                .AddType<GeoJsonMultiLineStringType>()
                .AddType<GeoJsonPolygonType>()
                .AddType<GeoJsonMultiPolygonType>()
                .AddType<GeoJsonGeometryEnumType>()
                .AddType<GeometryType>()
                .BindClrType<Coordinate, GeoJsonPositionType>();
        }

        public static IRequestExecutorBuilder AddSpatialTypes(
            this IRequestExecutorBuilder builder)
        {
            return builder.ConfigureSchema(x => x.AddSpatialTypes());
        }
    }
}
