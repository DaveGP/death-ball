using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;


namespace Builders
{
	/// <summary>
	/// Builds the browser version to Builds/WebGL, set up for static hosts like GitHub Pages: those
	/// can't send Content-Encoding headers, so the compressed files carry a JavaScript decompression
	/// fallback. Online play in the browser goes over Relay with secure WebSockets.
	/// </summary>
	public static class WebGLBuilder
	{
		public const string OutputFolder = "Builds/WebGL";

		[MenuItem("Death Ball/Build WebGL")]
		public static void BuildMenu() => Build();

		// Entry point for -batchmode -executeMethod
		public static void BuildBatch()
		{
			try
			{
				var succeeded = Build();
				EditorApplication.Exit(succeeded ? 0 : 1);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				EditorApplication.Exit(1);
			}
		}

		public static bool Build()
		{
			if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
				EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

			PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
			PlayerSettings.WebGL.decompressionFallback = true;
			PlayerSettings.WebGL.dataCaching = true;

			// Pages lets browsers cache files for ten minutes; hashed names stop a new deploy mixing with old files
			PlayerSettings.WebGL.nameFilesAsHashes = true;

			// Keeps an online match going while the tab is in the background
			PlayerSettings.runInBackground = true;
			PlayerSettings.defaultWebScreenWidth = 1280;
			PlayerSettings.defaultWebScreenHeight = 720;

			var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputFolder));

			if (Directory.Exists(output))
				Directory.Delete(output, true);

			var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
			{
				scenes = new[] { MainMenuSceneBuilder.ScenePath, ArenaSceneBuilder.ScenePath },
				locationPathName = output,
				target = BuildTarget.WebGL,
				options = BuildOptions.None
			});

			var summary = report.summary;

			if (summary.result != BuildResult.Succeeded)
			{
				Debug.LogError($"WebGLBuilder: Build {summary.result} with {summary.totalErrors} errors.");
				return false;
			}

			// Burst's debug symbols are for local profiling only and must not be published
			foreach (var debugFolder in Directory.GetDirectories(output, "*_DoNotShip"))
				Directory.Delete(debugFolder, true);

			// Stop GitHub Pages running the files through Jekyll
			File.WriteAllText(Path.Combine(output, ".nojekyll"), "");

			Debug.Log($"WebGLBuilder: Built {output} ({summary.totalSize / (1024f * 1024f):0.0} MB) in {summary.totalTime.TotalMinutes:0.0} minutes.");

			return true;
		}
	}
}
