using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Newtonsoft.Json;
public static class BuildSizeOptimized
{
 public static string Run()
 {
  if(EditorApplication.isPlaying||EditorApplication.isCompiling||EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("Build requires idle, compiling-error-free Edit mode");
  const string scene="Assets/HabitatIsland/Scenes/HabitatIsland.unity";
  if(!File.Exists(scene))throw new FileNotFoundException(scene);
  string folder=Path.GetFullPath("Builds/Windows_Playtest_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
  Directory.CreateDirectory(folder);
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},locationPathName=Path.Combine(folder,"HabitatIsland.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
  var evidence=new{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,durationSeconds=report.summary.totalTime.TotalSeconds,output=folder,unity=Application.unityVersion,scene,messages=report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception||m.type==LogType.Warning).Select(m=>new{type=m.type.ToString(),message=m.content}).ToArray()};
  string json=JsonConvert.SerializeObject(evidence,Formatting.Indented);File.WriteAllText("Verification/package-size-20261018/build-report.json",json);
  if(report.summary.result!=BuildResult.Succeeded)throw new Exception(json);
  return json;
 }
}

