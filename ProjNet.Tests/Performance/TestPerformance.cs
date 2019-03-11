﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using GeoAPI.CoordinateSystems.Transformations;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;
using NUnit.Framework;
using ProjNet;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace ProjNET.Tests.Performance
{
    public class PerformanceTests
    {
        private readonly CoordinateSystemServices _css = new CoordinateSystemServices(new CoordinateSystemFactory(), new CoordinateTransformationFactory());

        [TestCase(@"D:\Development\Source\Repos\NetTopologySuite\NetTopologySuite.Tests.NUnit\TestData\africa.wkt")]
        [TestCase(@"D:\Development\Source\Repos\NetTopologySuite\NetTopologySuite.Tests.NUnit\TestData\world.wkt")]
        public void TestPerformance(string pathToWktFile)
        {
            if (!System.IO.File.Exists(pathToWktFile))
                throw new IgnoreException($"File '{pathToWktFile}' not found.");

            Console.WriteLine(pathToWktFile);
            MathTransform.SequenceCoordinateConverter = null;
            
            DoTestPerformance(CoordinateArraySequenceFactory.Instance, pathToWktFile);
            DoTestPerformance(PackedCoordinateSequenceFactory.DoubleFactory, pathToWktFile);
            DoTestPerformance(DotSpatialAffineCoordinateSequenceFactory.Instance, pathToWktFile);
        }

        private void DoTestPerformance(ICoordinateSequenceFactory factory, string pathToWktFile, MathTransform.SequenceCoordinateConverterBase c = null)
        {
            var gf = new GeometryFactory(new PrecisionModel(PrecisionModels.Floating), 4326, factory);
            var wktFileReader = new WKTFileReader(pathToWktFile, new WKTReader(gf));
            if (c != null) MathTransform.SequenceCoordinateConverter = c;

            var geometries = wktFileReader.Read();
            var stopwatch = new Stopwatch();
            var transformed = new List<IGeometry>(geometries.Count);

            var mt = _css.CreateTransformation(GeographicCoordinateSystem.WGS84, ProjectedCoordinateSystem.WebMercator).MathTransform; 
            var gf2 = new GeometryFactory(new PrecisionModel(PrecisionModels.Floating), 3857, gf.CoordinateSequenceFactory);

            stopwatch.Start();
            foreach (var geometry in geometries)
            {
                transformed.Add(Transform(geometry, mt, gf2));
            }
            
            stopwatch.Stop();

            Console.WriteLine($"Transformation of {geometries.Count} geometries using {gf.CoordinateSequenceFactory.GetType().Name} took {stopwatch.ElapsedMilliseconds} ms");

        }

        private static IGeometry Transform(IGeometry geometry, IMathTransform transfrom, IGeometryFactory factory)
        {
            if (geometry is IGeometryCollection)
            {
                var res = new IGeometry[geometry.NumGeometries];
                for (int i = 0; i < geometry.NumGeometries; i++)
                    res[i] = Transform(geometry.GetGeometryN(i), transfrom, factory);
                return factory.BuildGeometry(res);
            }

            if (geometry is IPoint p)
                return factory.CreatePoint(transfrom.Transform(p.CoordinateSequence));

            if (geometry is ILineString l)
                return factory.CreateLineString(transfrom.Transform(l.CoordinateSequence));

            if (geometry is IPolygon po)
            {
                var holes = new ILinearRing[po.NumInteriorRings];
                for (int i = 0; i < po.NumInteriorRings; i++)
                {
                    var ring = CoordinateSequences.EnsureValidRing(factory.CoordinateSequenceFactory, transfrom.Transform(po.InteriorRings[i].CoordinateSequence));
                    holes[i] = factory.CreateLinearRing(ring);
                }

                var shell = CoordinateSequences.EnsureValidRing(
                    factory.CoordinateSequenceFactory, transfrom.Transform(po.ExteriorRing.CoordinateSequence));

                return CoordinateSequences.IsRing(shell)
                    ? factory.CreatePolygon(factory.CreateLinearRing(shell), holes)
                    : null;
            }

            throw new NotSupportedException();
        }
    }
}