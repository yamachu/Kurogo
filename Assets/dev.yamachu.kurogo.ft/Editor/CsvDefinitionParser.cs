using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace dev.yamachu.Kurogo.FT.Editor
{
    /// <summary>
    /// One target property parsed from a CSV row, before mesh/blendshape resolution.
    /// </summary>
    internal readonly struct ParsedTarget
    {
        public readonly string MeshName;
        public readonly string BlendShapeName;
        public readonly float MinValue;
        public readonly float MaxValue;

        public ParsedTarget(string meshName, string blendShapeName, float minValue, float maxValue)
        {
            MeshName = meshName;
            BlendShapeName = blendShapeName;
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }

    /// <summary>
    /// One drive parsed from the CSV: an OSC/animator parameter and the one or more mesh/blendshape
    /// targets it drives (multiple targets only arise in header mode - see <see cref="CsvDefinitionParser"/>).
    /// </summary>
    internal sealed class ParsedDrive
    {
        public string OscParameter;
        public float InputMin = 0f;
        public float InputMax = 1f;
        public float? Smoothing;
        public List<ParsedTarget> Targets = new List<ParsedTarget>();
    }

    /// <summary>
    /// Parses the KurogoFT definition CSV (v1 format).
    ///
    /// Header-driven layout: if the header row's columns are all recognized names (OSCParameter,
    /// MeshName, BlendShapeName required; Min, Max, InputMin, InputMax, Smoothing optional) with no
    /// duplicates, columns are read by name - in whatever order the header lists them - and rows
    /// sharing the same OSCParameter are merged into a single drive with multiple targets.
    ///
    /// Positional fallback: if the header doesn't match the known column set, it is discarded and
    /// data rows are read positionally as OSCParameter,MeshName,BlendShapeName (MinValue=0,
    /// MaxValue=100, InputMin=0, InputMax=1, no smoothing implied). This exists purely for internal
    /// compatibility - authors should just use recognized column names rather than relying on this
    /// fallback.
    ///
    /// Quoting is not supported (values containing commas are unsupported, in either mode).
    /// </summary>
    internal static class CsvDefinitionParser
    {
        private static readonly string[] RequiredColumns = { "OSCParameter", "MeshName", "BlendShapeName" };
        private static readonly string[] OptionalColumns = { "Min", "Max", "InputMin", "InputMax", "Smoothing" };

        public static List<ParsedDrive> Parse(string csvText, Action<int, string> reportWarning)
        {
            var result = new List<ParsedDrive>();
            if (string.IsNullOrEmpty(csvText)) return result;

            var lines = csvText.Split('\n');
            var headerLineIndex = -1;
            string[] headerFields = null;

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r').Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;

                headerLineIndex = i;
                headerFields = trimmed.Split(',').Select(f => f.Trim()).ToArray();
                break;
            }

            if (headerLineIndex < 0) return result; // no non-comment content at all

            var columnMap = TryBuildHeaderColumnMap(headerFields);
            return columnMap != null
                ? ParseHeaderMode(lines, headerLineIndex, columnMap, reportWarning)
                : ParsePositionalMode(lines, headerLineIndex, reportWarning);
        }

        private static Dictionary<string, int> TryBuildHeaderColumnMap(string[] headerFields)
        {
            var known = RequiredColumns.Concat(OptionalColumns);
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < headerFields.Length; i++)
            {
                var field = headerFields[i];
                if (!known.Contains(field, StringComparer.OrdinalIgnoreCase)) return null;
                if (map.ContainsKey(field)) return null; // duplicate column name
                map[field] = i;
            }

            return RequiredColumns.All(map.ContainsKey) ? map : null;
        }

        private static List<ParsedDrive> ParsePositionalMode(
            string[] lines, int headerLineIndex, Action<int, string> reportWarning)
        {
            var result = new List<ParsedDrive>();
            var seenParameters = new HashSet<string>(StringComparer.Ordinal);

            for (var i = headerLineIndex + 1; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                var trimmed = lines[i].TrimEnd('\r').Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;

                var fields = trimmed.Split(',');
                if (fields.Length < 3)
                {
                    reportWarning(lineNumber, $"expected 3 columns (OSCParameter,MeshName,BlendShapeName), got {fields.Length}; row skipped");
                    continue;
                }

                var oscParameter = fields[0].Trim();
                var meshName = fields[1].Trim();
                var blendShapeName = fields[2].Trim();

                if (oscParameter.Length == 0 || meshName.Length == 0 || blendShapeName.Length == 0)
                {
                    reportWarning(lineNumber, "one or more required fields are empty; row skipped");
                    continue;
                }

                if (!seenParameters.Add(oscParameter))
                {
                    reportWarning(lineNumber, $"duplicate OSCParameter '{oscParameter}'; row skipped (first occurrence wins)");
                    continue;
                }

                result.Add(new ParsedDrive
                {
                    OscParameter = oscParameter,
                    InputMin = 0f,
                    InputMax = 1f,
                    Targets = { new ParsedTarget(meshName, blendShapeName, 0f, 100f) }
                });
            }

            return result;
        }

        private static List<ParsedDrive> ParseHeaderMode(
            string[] lines, int headerLineIndex, Dictionary<string, int> columnMap, Action<int, string> reportWarning)
        {
            var drives = new List<ParsedDrive>();
            var driveIndexByParam = new Dictionary<string, int>(StringComparer.Ordinal);
            var seenTargetKeys = new HashSet<string>(StringComparer.Ordinal);

            var oscIdx = columnMap["OSCParameter"];
            var meshIdx = columnMap["MeshName"];
            var shapeIdx = columnMap["BlendShapeName"];

            for (var i = headerLineIndex + 1; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                var trimmed = lines[i].TrimEnd('\r').Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;

                var fields = trimmed.Split(',').Select(f => f.Trim()).ToArray();
                var maxRequiredIdx = Math.Max(oscIdx, Math.Max(meshIdx, shapeIdx));
                if (fields.Length <= maxRequiredIdx)
                {
                    reportWarning(lineNumber, $"row has fewer columns than the header requires; row skipped");
                    continue;
                }

                var oscParameter = fields[oscIdx];
                var meshName = fields[meshIdx];
                var blendShapeName = fields[shapeIdx];

                if (oscParameter.Length == 0 || meshName.Length == 0 || blendShapeName.Length == 0)
                {
                    reportWarning(lineNumber, "one or more required fields are empty; row skipped");
                    continue;
                }

                var inputMin = ReadOptionalFloat(fields, columnMap, "InputMin", 0f, lineNumber, reportWarning);
                var inputMax = ReadOptionalFloat(fields, columnMap, "InputMax", 1f, lineNumber, reportWarning);
                var minValue = ReadOptionalFloat(fields, columnMap, "Min", 0f, lineNumber, reportWarning);
                var maxValue = ReadOptionalFloat(fields, columnMap, "Max", 100f, lineNumber, reportWarning);
                var smoothing = ReadOptionalSmoothing(fields, columnMap, lineNumber, reportWarning);

                var targetKey = $"{oscParameter}|{meshName}|{blendShapeName}";
                if (!seenTargetKeys.Add(targetKey))
                {
                    reportWarning(lineNumber, $"duplicate row for parameter '{oscParameter}' targeting {meshName}/{blendShapeName}; row skipped");
                    continue;
                }

                if (driveIndexByParam.TryGetValue(oscParameter, out var existingIdx))
                {
                    var existing = drives[existingIdx];
                    if (existing.InputMin != inputMin || existing.InputMax != inputMax)
                    {
                        reportWarning(lineNumber, $"duplicate OSCParameter '{oscParameter}' specifies different InputMin/InputMax; using the first occurrence's values");
                    }

                    if (existing.Smoothing != smoothing)
                    {
                        reportWarning(lineNumber, $"duplicate OSCParameter '{oscParameter}' specifies different Smoothing; using the first occurrence's value");
                    }

                    existing.Targets.Add(new ParsedTarget(meshName, blendShapeName, minValue, maxValue));
                }
                else
                {
                    driveIndexByParam[oscParameter] = drives.Count;
                    drives.Add(new ParsedDrive
                    {
                        OscParameter = oscParameter,
                        InputMin = inputMin,
                        InputMax = inputMax,
                        Smoothing = smoothing,
                        Targets = { new ParsedTarget(meshName, blendShapeName, minValue, maxValue) }
                    });
                }
            }

            return drives;
        }

        private static float ReadOptionalFloat(
            string[] fields, Dictionary<string, int> columnMap, string columnName, float defaultValue,
            int lineNumber, Action<int, string> reportWarning)
        {
            if (!columnMap.TryGetValue(columnName, out var idx) || idx >= fields.Length) return defaultValue;

            var raw = fields[idx];
            if (raw.Length == 0) return defaultValue;

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;

            reportWarning(lineNumber, $"'{columnName}' value '{raw}' is not a valid number; using default {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// Reads the optional Smoothing column. Absent/empty -> null (no smoothing). An invalid or
        /// out-of-range value warns and falls back to null - Kurogo Core requires LocalSmoothness to be
        /// in [0, 1), so this keeps a bad CSV value from throwing at build time.
        /// </summary>
        private static float? ReadOptionalSmoothing(
            string[] fields, Dictionary<string, int> columnMap, int lineNumber, Action<int, string> reportWarning)
        {
            if (!columnMap.TryGetValue("Smoothing", out var idx) || idx >= fields.Length) return null;

            var raw = fields[idx];
            if (raw.Length == 0) return null;

            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                reportWarning(lineNumber, $"'Smoothing' value '{raw}' is not a valid number; smoothing disabled for this row");
                return null;
            }

            if (value < 0f || value >= 1f)
            {
                reportWarning(lineNumber, $"'Smoothing' value '{value}' must be in [0, 1); smoothing disabled for this row");
                return null;
            }

            return value;
        }
    }
}
