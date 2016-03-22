using Azi.Cloud.Common;
using Azi.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Azi.Cloud.DokanNet.Gui
{
    public class AvailableCloudsModel
    {
        static AvailableCloudsModel()
        {
            AvailableClouds = new List<AvailableCloud>();
            foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Clouds.*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);

                    var types = assembly.GetExportedTypes().Where(t => typeof(IHttpCloud).IsAssignableFrom(t));

                    AvailableClouds.AddRange(types.Where(t => t.IsClass)
                            .Select(t => new AvailableCloud
                            {
                                AssemblyName = t.Assembly.FullName,
                                ClassName = t.FullName,
                                Name = (string)t.GetProperty("CloudServiceName").GetValue(null),
                                Icon = (string)t.GetProperty("CloudServiceIcon").GetValue(null)
                            }));
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }

        public static List<AvailableCloud> AvailableClouds { get; }

        public class AvailableCloud
        {
            public string ClassName { get; set; }

            public string Name { get; set; }

            public string Icon { get; set; }

            public string AssemblyName { get; set; }
        }
    }
}
