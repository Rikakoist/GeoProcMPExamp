using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.SpatialAnalystTools;

namespace MPChild
{
    public class ReclassFunc
    {
        public ReclassByTable rBT = new ReclassByTable();
        Geoprocessor GP = new Geoprocessor()
        {
            AddOutputsToMap = false,
            OverwriteOutput = true
        };

        public ReclassFunc(string inRaster, string inRemapTable, string outRaster)
        {
            rBT.in_raster = inRaster;
            rBT.in_remap_table = inRemapTable;
            rBT.out_raster = outRaster;
            rBT.from_value_field = "FROM";
            rBT.to_value_field = "TO";
            rBT.output_value_field = "OUT";
            rBT.missing_values = "DATA";
        }

        public void Exec()
        {
            GP.Execute(rBT, null);
        }    
    }

    public class AggregateFunc
    {
        public Aggregate a = new Aggregate();
        Geoprocessor GP = new Geoprocessor()
        {
            AddOutputsToMap = false,
            OverwriteOutput = true
        };

        public AggregateFunc(string inRaster, int cellFactor, string outRaster)
        {
            a.in_raster = inRaster;
            a.cell_factor = cellFactor;
            a.out_raster = outRaster;
            a.aggregation_type = "SUM";
            a.extent_handling = "TRUNCATE";
            a.ignore_nodata = "DATA";
        }

        public void Exec()
        {
            GP.Execute(a, null);
        }
    }

    public class DivideFunc
    {
        public Divide d = new Divide();
        Geoprocessor GP = new Geoprocessor()
        {
            AddOutputsToMap = false,
            OverwriteOutput = true
        };

        public DivideFunc(string inRaster01, string inRaster02, string outRaster)
        {
            d.in_raster_or_constant1 = inRaster01;
            d.in_raster_or_constant2 = inRaster02;
            d.out_raster = outRaster;      
        }

        public void Exec()
        {
            GP.Execute(d, null);
        }
    }
}
