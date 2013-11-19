﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AshMind.Extensions;
using DependencyInjection.FeatureTables.Generator.Data;
using DependencyInjection.FeatureTables.Generator.Sources.FeatureTestSupport;
using DependencyInjection.FeatureTests;
using DependencyInjection.FeatureTests.Documentation;

namespace DependencyInjection.FeatureTables.Generator.Sources {
    public class FeatureTestTableSource : IFeatureTableSource {
        private readonly FeatureTestRunner runner;

        public FeatureTestTableSource(FeatureTestRunner runner) {
            this.runner = runner;
        }

        public IEnumerable<FeatureTable> GetTables() {
            var testRuns = this.runner.RunAllTests(typeof(BasicTests).Assembly).ToDictionary(r => new { Test = r.Method, r.FrameworkType });
            var testGroups = testRuns.Keys
                                     .Select(k => k.Test)
                                     .Distinct()
                                     .GroupBy(m => m.DeclaringType)
                                     .OrderBy(g => this.GetDisplayOrder(g.Key))
                                     .ToArray();

            var frameworks = Frameworks.Enumerate().ToArray();
            foreach (var group in testGroups) {
                var features = group.ToDictionary(m => m, this.ConvertToFeature);
                var table = new FeatureTable(AttributeHelper.GetDisplayName(group.Key), frameworks, features.Values) {
                    Description = this.GetDescription(@group.Key),
                    Scoring = AttributeHelper.GetScoring(@group.Key)
                };

                var resultApplyTasks = new List<Task>();
                foreach (var test in group.OrderBy(this.GetDisplayOrder)) {
                    foreach (var framework in frameworks) {
                        var cell = table[framework, test];
                        var run = testRuns[new { Test = test, FrameworkType = framework.GetType() }];

                        resultApplyTasks.Add(ApplyRunResultToCell(cell, run.Task));
                    }
                }

                Task.WaitAll(resultApplyTasks.ToArray());
                yield return table;
            }
        }

        private async Task ApplyRunResultToCell(FeatureCell cell, Task<FeatureTestResult> resultTask) {
            var result = await resultTask;
            if (result.Kind == FeatureTestResultKind.Success) {
                cell.DisplayValue = "supported";
                cell.State = FeatureState.Success;
            }
            else if (result.Kind == FeatureTestResultKind.Failure) {
                cell.DisplayValue = "failed";
                cell.State = FeatureState.Failure;
                var exceptionString = RemoveLocalPaths(result.Exception.ToString());
                cell.RawError = exceptionString;
                cell.DisplayUri = ConvertToDataUri(exceptionString);
            }
            else if (result.Kind == FeatureTestResultKind.SkippedDueToDependency) {
                cell.DisplayValue = "skipped";
                cell.State = FeatureState.Skipped;
            }
            else {
                cell.DisplayValue = "see comment";
                cell.State = FeatureState.Concern;
            }
            cell.Comment = result.Message;
        }

        private Feature ConvertToFeature(MethodInfo test) {
            return new Feature(test, AttributeHelper.GetDisplayName(test)) { Description = this.GetDescription(test) };
        }

        private string GetDescription(MemberInfo member) {
            var description = member.GetCustomAttributes<DescriptionAttribute>().Select(a => a.Description).SingleOrDefault();
            if (description.IsNullOrEmpty())
                return description;

            // remove all spaces at the start of the line
            return Regex.Replace(description, @"^ +", "", RegexOptions.Multiline);
        }

        private int GetDisplayOrder(MemberInfo member) {
            var displayOrderAttribute = member.GetCustomAttributes<DisplayOrderAttribute>().SingleOrDefault();
            if (displayOrderAttribute == null)
                return int.MaxValue;

            return displayOrderAttribute.Order;
        }
        
        /// <summary>
        /// Removes all paths that are potentially local.
        /// </summary>
        private string RemoveLocalPaths(string exceptionString) {
            return Regex.Replace(exceptionString, @"(?<=\W|^)((?:\w\:|\\\\)[^:\r\n]+)", match => {
                var path = match.Groups[1].Value;
                if (File.Exists(path))
                    return Path.Combine("[removed]", Path.GetFileName(path));

                if (Directory.Exists(path))
                    return "[removed]";

                return match.Value;
            });
        }

        private Uri ConvertToDataUri(string value) {
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            return new Uri("data:text/plain;base64," + base64);
        }
    }
}