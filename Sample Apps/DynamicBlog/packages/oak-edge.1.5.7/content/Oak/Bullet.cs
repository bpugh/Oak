﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;

namespace Oak
{
    public class SqlQueryLog
    {
        public SqlQueryLog(object sender, string query, string stackTrace, int threadId, object[] args)
        {
            Sender = sender;
            Query = query;
            StackTrace = stackTrace;
            ThreadId = threadId;
            Args = args;
        }

        public object Sender { get; set; }
        public string Query { get; set; }
        public string StackTrace { get; set; }
        public int ThreadId { get; set; }
        public object[] Args { get; set; }
    }

    public class Bullet
    {
        static string redundantQueryErrorMessage =
@"This query is redundant, consider cacheing the value returned.  
This may be a possible N+1 issue, try eager loading...if you 
are using DynamicRepository, use the Include() method, for example: 
comments.All().Include(""Blog"").";

        static string nPlusOneQueryErrorMessage =
@"Possible N+1, try eager loading...if you are using 
DynamicRepository, use the Include() method, for example: 
blogs.All().Include(""Comments"", ""Tags"").";

        public static IEnumerable<dynamic> InefficientQueries(List<SqlQueryLog> log)
        {
            var inefficientQueries = new List<dynamic>();

            var lookup = new HashSet<Guid>();

            var logWithIds = new List<dynamic>();

            for (int i = 0; i < log.Count; i++)
            {
                var record = log[i];

                logWithIds.Add(new Gemini(new
                {
                    Id = Guid.NewGuid(),
                    record.Query,
                    record.StackTrace,
                    record.ThreadId
                }));
            }

            var comparisons =
                from first in logWithIds
                from second in logWithIds
                select new { first, second };

            foreach (var comparison in comparisons)
            {
                var first = comparison.first;
                var second = comparison.second;

                if (first.Id == second.Id) continue;

                if (IsExactStringMatch(first.Query, second.Query))
                {
                    AddQuery(inefficientQueries, first, redundantQueryErrorMessage, lookup);

                    AddQuery(inefficientQueries, second, redundantQueryErrorMessage, lookup);
                }
                else if (IsSqlSimilar(first.Query, second.Query))
                {
                    AddQuery(inefficientQueries, first, nPlusOneQueryErrorMessage, lookup);

                    AddQuery(inefficientQueries, second, nPlusOneQueryErrorMessage, lookup);
                }
            }

            return new DynamicModels(inefficientQueries);
        }

        public static void AddQuery(List<dynamic> inefficientQueries, dynamic queryLog, string reason, HashSet<Guid> lookup)
        {
            if (!lookup.Contains(queryLog.Id))
            {
                inefficientQueries.Add(new Gemini(new
                {
                    Id = queryLog.Id,
                    Query = queryLog.Query,
                    Reason = reason,
                    StackTrace = ScrubStackTrace(queryLog.StackTrace),
                    ThreadId = queryLog.ThreadId
                }));

                lookup.Add(queryLog.Id);
            }
        }

        public static bool IsSqlSimilar(string first, string second)
        {
            return SqlExcludingInStatement(first) == SqlExcludingInStatement(second);
        }

        private static bool IsExactStringMatch(string first, string second)
        {
            return first == second;
        }

        public static string SqlExcludingInStatement(string sql)
        {
            return Regex.Replace(sql, @" in \([^)]*\)", "");
        }

        public static IEnumerable<T> EachConsecutive2<T>(IEnumerable<T> source, Action<T, T> action)
        {
            var array = source.ToArray();
            for (int i = 0; i < array.Length - 1; i++)
            {
                action(array[i], array[i + 1]);
            }

            return source;
        }

        public static string ScrubStackTrace(string stackTrace)
        {
            var sb = new StringBuilder();

            using (StringReader sr = new StringReader(stackTrace))
            {
                var line = null as string;

                while ((line = sr.ReadLine()) != null)
                {
                    if (new List<string>
                    {
                        "at System.",
                        "at Oak.",
                        "at lambda_method",
                        "at Massive.",
                        "at Castle.",
                        "at CallSite.",
                        "at Glimpse."
                    }.Any(s => line.Contains(s))) continue;

                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }
    }
}