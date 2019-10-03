using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace shared
{
    public static class Args
    {
        public static bool ReadFlag(
            this List<string> args,
            params string[] flags)
        {
            var found = args.Where(flags.Contains).ToArray();
            foreach (var flag in found)
            {
                args.Remove(flag);
            }

            return found.Any();
        }

        public static string[] ReadParameter(
            this List<string> args,
            ParameterOptions options,
            params string[] parameters)
        {
            var inParam = false;
            var newArgs = new List<string>();
            var result = args.Aggregate(
                new List<string>(),
                (acc, cur) =>
                {
                    if (cur.StartsWith("-"))
                    {
                        inParam = false;
                    }
                    
                    if (inParam)
                    {
                        return Consume(acc, cur);
                    }
                    
                    var isParam = parameters.Contains(cur);
                    return isParam
                        ? WatchForValues(acc, cur)
                        : Retain(acc, cur);
                });
            
            args.Clear();
            args.AddRange(newArgs);
            
            foreach (var p in parameters)
            {
                result.RemoveAll(a => a == p);
            }

            return result.ToArray();

            List<string> Retain(List<string> acc, string cur)
            {
                newArgs.Add(cur);
                return acc;
            }

            List<string> WatchForValues(List<string> acc, string cur)
            {
                inParam = true;
                acc.Add(cur);
                return acc;
            }

            List<string> Consume(List<string> acc, string cur)
            {
                acc.Add(cur);
                if (options == ParameterOptions.Conservative)
                {
                    inParam = false;
                }
                return acc;
            }
        }
    }

    public enum ParameterOptions
    {
        // requires syntax like --parameter value1 --parameter value2
        Conservative,
        // requires syntax like --parameter value1 value2 --another-parameter
        //  - should allow termination with standalone --
        Greedy
    }
}