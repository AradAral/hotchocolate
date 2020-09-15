﻿﻿using NetTopologySuite.Geometries;
using static HotChocolate.Types.Spatial.Properties.Resources;

namespace HotChocolate.Types.Spatial
{
    public class GeoJsonMultiPointType : ObjectType<MultiPoint>
    {
        protected override void Configure(IObjectTypeDescriptor<MultiPoint> descriptor)
        {
            descriptor.GeoJsonName(nameof(GeoJsonMultiPointType));

            descriptor.BindFieldsExplicitly();

            descriptor.Implements<GeoJsonInterface>();

            descriptor.Field(x => x.Coordinates)
                .Description(GeoJson_Field_Coordinates_Description_MultiPoint);
            descriptor.Field<GeoJsonResolvers>(x => x.GetType(default!))
                .Description(GeoJson_Field_Type_Description);
            descriptor.Field<GeoJsonResolvers>(x => x.GetBbox(default!))
                .Description(GeoJson_Field_Bbox_Description);
            descriptor.Field<GeoJsonResolvers>(x => x.GetCrs(default!))
                .Description(GeoJson_Field_Crs_Description);
        }
    }
}
