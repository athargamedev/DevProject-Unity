using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    /// <summary>
    /// Resolves DiagnosticBreakpointAnchor SearchHint patterns to actual line numbers in source files.
    /// Uses simple pattern matching to avoid heavy LSP dependency in runtime builds.
    /// </summary>
    public static class DiagnosticBreakpointAnchorResolver
    {
        private static readonly Dictionary<string, ResolvedAnchor> s_Cache =
            new Dictionary<string, ResolvedAnchor>(StringComparer.Ordinal);

        private static readonly Regex s_MethodPattern = new Regex(
            @"(private|public|protected|internal|static|\s)+[\w<>[\],\s]+\s+(\w+)\s*\(",
            RegexOptions.Compiled);

        private struct ResolvedAnchor
        {
            public int LineNumber;
            public string FilePath;
            public bool Found;
        }

        /// <summary>
        /// Attempt to resolve a breakpoint anchor to its actual file location and line number.
        /// Results are cached for performance.
        /// </summary>
        /// <param name="anchor">The breakpoint anchor to resolve.</param>
        /// <param name="lineNumber">The resolved line number, or -1 if not found.</param>
        /// <param name="filePath">The resolved file path, or empty if not found.</param>
        /// <returns>True if the anchor was resolved successfully.</returns>
        public static bool TryResolve(DiagnosticBreakpointAnchor anchor, out int lineNumber, out string filePath)
        {
            lineNumber = -1;
            filePath = string.Empty;

            if (anchor.Equals(default(DiagnosticBreakpointAnchor)) || string.IsNullOrWhiteSpace(anchor.Id))
            {
                return false;
            }

            if (s_Cache.TryGetValue(anchor.Id, out ResolvedAnchor cached))
            {
                lineNumber = cached.LineNumber;
                filePath = cached.FilePath;
                return cached.Found;
            }

            bool resolved = TryResolveInternal(anchor, out lineNumber, out filePath);
            s_Cache[anchor.Id] = new ResolvedAnchor
            {
                LineNumber = lineNumber,
                FilePath = filePath,
                Found = resolved,
            };

            return resolved;
        }

        /// <summary>
        /// Resolve an anchor by its ID string.
        /// </summary>
        public static bool TryResolveById(string anchorId, out int lineNumber, out string filePath)
        {
            if (DiagnosticBreakpointAnchors.TryGet(anchorId, out DiagnosticBreakpointAnchor anchor))
            {
                return TryResolve(anchor, out lineNumber, out filePath);
            }

            lineNumber = -1;
            filePath = string.Empty;
            return false;
        }

        /// <summary>
        /// Clear the resolution cache. Call this if files have changed.
        /// </summary>
        public static void ClearCache()
        {
            s_Cache.Clear();
        }

        private static bool TryResolveInternal(DiagnosticBreakpointAnchor anchor, out int lineNumber, out string filePath)
        {
            lineNumber = -1;
            filePath = string.Empty;

            if (string.IsNullOrWhiteSpace(anchor.RelativeFilePath) || string.IsNullOrWhiteSpace(anchor.SearchHint))
            {
                return false;
            }

            string fullPath = Path.Combine(Application.dataPath, "..", anchor.RelativeFilePath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(fullPath);
                string searchHint = anchor.SearchHint.Trim();

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (line.Contains(searchHint, StringComparison.Ordinal))
                    {
                        lineNumber = i + 1;
                        filePath = Path.GetFullPath(fullPath);
                        return true;
                    }

                    if (s_MethodPattern.IsMatch(searchHint))
                    {
                        Match match = s_MethodPattern.Match(searchHint);
                        if (match.Success && match.Groups.Count > 2)
                        {
                            string methodName = match.Groups[2].Value;
                            if (line.Contains(methodName, StringComparison.Ordinal))
                            {
                                lineNumber = i + 1;
                                filePath = Path.GetFullPath(fullPath);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Resolve all known anchors and return a dictionary of ID to resolved location.
        /// </summary>
        public static Dictionary<string, (int lineNumber, string filePath)> ResolveAll()
        {
            var results = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
            DiagnosticBreakpointAnchor[] anchors = DiagnosticBreakpointAnchors.GetAll();

            if (anchors == null)
            {
                return results;
            }

            foreach (DiagnosticBreakpointAnchor anchor in anchors)
            {
                if (TryResolve(anchor, out int lineNumber, out string filePath))
                {
                    results[anchor.Id] = (lineNumber, filePath);
                }
            }

            return results;
        }
    }
}
