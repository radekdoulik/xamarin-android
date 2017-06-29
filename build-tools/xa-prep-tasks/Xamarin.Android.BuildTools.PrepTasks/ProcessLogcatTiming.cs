using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class ProcessLogcatTiming : Task
	{
		public string Logcat { get; set; }

		public string Application { get; set; }

		public override bool Execute ()
		{
			using (var reader = new StreamReader (Logcat)) {
				string line;
				int pid = -1;
				var procStartRegex = new Regex ($"^(\\d+-\\d+\\s+[\\d\\:\\.]+)\\s+.*ActivityManager: Start proc.*for added application {Application}\\: pid=(\\d+)");
				Regex timingRegex = null;
				var runtimeInitRegex = new Regex ("Runtime\\.init\\: end native-to-managed");
				DateTime start = DateTime.Now;
				DateTime last = start;
				DateTime initEnd = start;

				while ((line = reader.ReadLine ()) != null) {
					if (pid == -1) {
						var match = procStartRegex.Match (line);
						if (match.Success) {
							start = ParseTime (match.Groups [1].Value);
							pid = Int32.Parse (match.Groups [2].Value);
							Log.LogMessage (MessageImportance.Low, $"Time:      0ms process start, application: '{Application}' PID: {pid}");
							timingRegex = new Regex ($"^(\\d+-\\d+\\s+[\\d\\:\\.]+)\\s+{pid}\\s+.*I monodroid-timing\\:\\s(.*)$");
						}
					} else {
						var match = timingRegex.Match (line);
						if (match.Success) {
							var time = ParseTime (match.Groups [1].Value);
							var span = time - start;
							Log.LogMessage (MessageImportance.Low, $"Time: {span.TotalMilliseconds.ToString ().PadLeft (6)}ms Message: {match.Groups [2].Value}");

							match = runtimeInitRegex.Match (match.Groups [2].Value);
							if (match.Success)
								initEnd = time;
							last = time;
						}
					}
				}

				if (pid != -1) {
					Log.LogMessage (MessageImportance.Normal, " -- Performance summary --");
					Log.LogMessage (MessageImportance.Normal, $"Runtime init end: {(initEnd - start).TotalMilliseconds}ms");
					Log.LogMessage (MessageImportance.Normal, $"Last timing message: {(last - start).TotalMilliseconds}ms");
				} else
					Log.LogWarning ("Wasn't able to collect the performance data");

				reader.Close ();
			}

			return true;
		}

		static Regex timeRegex = new Regex ("(\\d+)-(\\d+)\\s+(\\d+)\\:(\\d+)\\:(\\d+)\\.(\\d+)");
		DateTime ParseTime (string s)
		{
			var match = timeRegex.Match (s);
			if (match.Success)
				// we don't handle year boundary here as the logcat timestamp doesn't include year information
				return new DateTime (DateTime.Now.Year,
						     int.Parse (match.Groups [1].Value),
						     int.Parse (match.Groups [2].Value),
						     int.Parse (match.Groups [3].Value),
						     int.Parse (match.Groups [4].Value),
						     int.Parse (match.Groups [5].Value),
						     int.Parse (match.Groups [6].Value));

			throw new Exception ("unable to parse time");
		}
	}
}
