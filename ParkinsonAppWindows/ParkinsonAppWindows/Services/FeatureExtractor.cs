using System;
using System.Collections.Generic;
using System.Linq;
using ParkinsonAppWindows.Models;

namespace ParkinsonAppWindows.Services
{
    public class ExtractionResult
    {
        public float[] Features { get; set; } 
        public double Duration { get; set; }  
        public double MeanPressure { get; set; }
        public double MeanAltitude { get; set; }
        public double MeanAzimuth { get; set; }
    }

    public class FeatureExtractor
    {
        private const double PPI = 264.0;
        private const double MM_SCALE = 25.4 / PPI;

        public static readonly string[] FEATURES_ORDER = new[]
        {
            "rotation_angle_mass", "duration", "pressure_median", "slopes_min",
            "velocity_median", "alpha_accel_max", "alpha_accel_min", "snatch_mean",
            "shake_mass", "shake_max", "shake_mean", "shake_median",
            "snap_mass", "crackle_mass", "pop_mass", "accel_x_mass",
            "accel_x_min", "pressure_diff_min", "pressure_diff_max", "alpha_velocity_max"
        };

        public ExtractionResult ExtractFeatures(DrawingDataFile dataFile)
        {
            // 1. Собираем все точки в один список
            var rawPoints = new List<PointData>();
            if (dataFile.Data != null)
            {
                foreach (var stroke in dataFile.Data)
                {
                    foreach (var p in stroke)
                    {
                        rawPoints.Add(new PointData { T = p.T, X = p.X, Y = p.Y, P = p.P, A = p.A, L = p.L });
                    }
                }
            }

            if (rawPoints.Count < 2)
                return new ExtractionResult { Features = new float[FEATURES_ORDER.Length] };

            rawPoints = rawPoints.OrderBy(p => p.T).ToList();

            
            double totalRawTime = rawPoints.Last().T - rawPoints.First().T;
            double timeScale = (totalRawTime > 1000.0) ? 0.001 : 1.0;

            

            var clean = new List<ProcessedPoint>();

            for (int i = 1; i < rawPoints.Count; i++)
            {
                double dt = (rawPoints[i].T - rawPoints[i - 1].T) * timeScale;

                if (dt > 0.0001)
                {
                    clean.Add(new ProcessedPoint
                    {
                        T_sec = rawPoints[i].T * timeScale, 
                        Dt = dt,
                        P = rawPoints[i].P,
                        Dx = (rawPoints[i].X - rawPoints[i - 1].X) * MM_SCALE,
                        Dy = (rawPoints[i].Y - rawPoints[i - 1].Y) * MM_SCALE,
                        Azimuth = rawPoints[i].A,
                        Altitude = rawPoints[i].L
                    });
                }
            }

            if (clean.Count < 5)
                return new ExtractionResult { Features = new float[FEATURES_ORDER.Length] };

           
            double computedDuration = clean.Last().T_sec - clean.First().T_sec;

            var dtList = clean.Select(x => x.Dt).ToList();
            var dxList = clean.Select(x => x.Dx).ToList();
            var dyList = clean.Select(x => x.Dy).ToList();
            var pList = clean.Select(x => x.P).ToList();
            var aList = clean.Select(x => x.Azimuth).ToList();
            var lList = clean.Select(x => x.Altitude).ToList();

            var velocity = new List<double>();
            var velocity_x = new List<double>();
            var alpha = new List<double>();
            var slopes = new List<double>();

            for (int i = 0; i < clean.Count; i++)
            {
                double dist = Math.Sqrt(dxList[i] * dxList[i] + dyList[i] * dyList[i]);
                velocity.Add(dist / dtList[i]);
                velocity_x.Add(dxList[i] / dtList[i]);
                alpha.Add(Math.Atan2(dyList[i], dxList[i]));
                slopes.Add(Math.Abs(dxList[i]) < 1e-9 ? 0 : dyList[i] / dxList[i]);
            }

            var accel_x = GetDerivative(velocity_x, dtList);
            var accel = GetDerivative(velocity, dtList);
            var jerk = GetDerivative(accel, dtList);
            var snap = GetDerivative(jerk, dtList);
            var crackle = GetDerivative(snap, dtList);
            var pop = GetDerivative(crackle, dtList);

            var pressure_diff = GetDiff(pList);
            var yank = GetDerivative(pList, dtList);
            var tug = GetDerivative(yank, dtList);
            var snatch = GetDerivative(tug, dtList);
            var shake = GetDerivative(snatch, dtList);

            var alpha_velocity = GetDerivative(alpha, dtList);
            var alpha_accel = GetDerivative(alpha_velocity, dtList);

            var phi = new List<double>();
            for (int i = 1; i < alpha.Count; i++) phi.Add(Math.PI + alpha[i - 1] - alpha[i]);

            var feats = new Dictionary<string, double>();
            feats["duration"] = computedDuration;

            AddStats(feats, "velocity", velocity);
            AddStats(feats, "accel_x", accel_x);
            AddStats(feats, "snap", snap);
            AddStats(feats, "crackle", crackle);
            AddStats(feats, "pop", pop);
            AddStats(feats, "pressure", pList);
            AddStats(feats, "pressure_diff", pressure_diff);
            AddStats(feats, "snatch", snatch);
            AddStats(feats, "shake", shake);
            AddStats(feats, "alpha_velocity", alpha_velocity);
            AddStats(feats, "alpha_accel", alpha_accel);

            feats["rotation_angle_mass"] = Mass(phi);
            feats["slopes_min"] = slopes.Any() ? slopes.Min() : 0;

            var resultVector = new float[FEATURES_ORDER.Length];
            for (int i = 0; i < FEATURES_ORDER.Length; i++)
            {
                string key = FEATURES_ORDER[i];
                if (feats.TryGetValue(key, out double val))
                {
                    if (double.IsNaN(val) || double.IsInfinity(val)) resultVector[i] = 0f;
                    else resultVector[i] = (float)val;
                }
                else
                {
                    resultVector[i] = 0f;
                }
            }

            return new ExtractionResult
            {
                Features = resultVector,
                Duration = computedDuration,
                MeanPressure = pList.Any() ? pList.Average() : 0,
                MeanAltitude = lList.Any() ? lList.Average() : 0,
                MeanAzimuth = aList.Any() ? aList.Average() : 0
            };
        }

        private List<double> GetDerivative(List<double> s, List<double> dt)
        {
            var res = new List<double>();
            for (int i = 1; i < s.Count; i++)
            {
                double t = dt[i] <= 0 ? 0.001 : dt[i];
                res.Add((s[i] - s[i - 1]) / t);
            }
            return res;
        }

        private List<double> GetDiff(List<double> s)
        {
            var res = new List<double>();
            for (int i = 1; i < s.Count; i++) res.Add(s[i] - s[i - 1]);
            return res;
        }

        private void AddStats(Dictionary<string, double> dict, string name, List<double> vals)
        {
            if (vals.Count == 0) return;
            dict[$"{name}_mean"] = vals.Average();
            dict[$"{name}_median"] = Median(vals);
            dict[$"{name}_min"] = vals.Min();
            dict[$"{name}_max"] = vals.Max();
            dict[$"{name}_mass"] = Mass(vals);
            dict[$"{name}_std"] = StdDev(vals); // Добавлено, т.к. используется в analyzer.py
        }

        private double Mass(List<double> v) => v.Sum(x => Math.Abs(x));

        private double Median(List<double> v)
        {
            if (!v.Any()) return 0;
            var s = v.OrderBy(x => x).ToList();
            int m = s.Count / 2;
            return s.Count % 2 != 0 ? s[m] : (s[m - 1] + s[m]) / 2.0;
        }

        private double StdDev(List<double> v)
        {
            if (v.Count < 2) return 0;
            double avg = v.Average();
            return Math.Sqrt(v.Sum(d => Math.Pow(d - avg, 2)) / (v.Count - 1));
        }

        // Внутренние классы данных
        private class PointData { public double T, X, Y, P, A, L; }
        private class ProcessedPoint
        {
            public double T_sec; // Время в секундах
            public double Dt, Dx, Dy, P, Azimuth, Altitude;
        }
    }
}