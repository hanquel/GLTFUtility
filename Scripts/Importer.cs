using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using System.Diagnostics;

namespace Siccity.GLTFUtility {
	/// <summary> API used for importing .gltf and .glb files </summary>
	public static class Importer {
		// hkyoo
		public static GLTFObject LoadFromFileGLTFObject(string filepath, out long binChunk)
		{
			// TODO : 코드 정리 필요 (테스트 용)
			return ImportGLB(filepath, out binChunk);
		}

		public static GameObject LoadFromFile(string filepath, Format format = Format.AUTO) {
			AnimationClip[] animations;
			return LoadFromFile(filepath, new ImportSettings(), out animations, format);
		}

		public static GameObject LoadFromFile(string filepath, ImportSettings importSettings, Format format = Format.AUTO) {
			AnimationClip[] animations;
			return LoadFromFile(filepath, importSettings, out animations, format);
		}

		public static GameObject LoadFromFile(string filepath, ImportSettings importSettings, out AnimationClip[] animations, Format format = Format.AUTO) {
			if (format == Format.GLB) {
				return ImportGLB(filepath, importSettings, out animations);
			} else if (format == Format.GLTF) {
				return ImportGLTF(filepath, importSettings, out animations);
			} else {
				string extension = Path.GetExtension(filepath).ToLower();
				if (extension == ".glb") return ImportGLB(filepath, importSettings, out animations);
				else if (extension == ".gltf") return ImportGLTF(filepath, importSettings, out animations);
				else {
					UnityEngine.Debug.Log("Extension '" + extension + "' not recognized in " + filepath);
					animations = null;
					return null;
				}
			}
		}

		/// <param name="bytes">GLB file is supported</param>
		public static GameObject LoadFromBytes(byte[] bytes, ImportSettings importSettings = null) {
			AnimationClip[] animations;
			if (importSettings == null) importSettings = new ImportSettings();
			return ImportGLB(bytes, importSettings, out animations);
		}

		/// <param name="bytes">GLB file is supported</param>
		public static GameObject LoadFromBytes(byte[] bytes, ImportSettings importSettings, out AnimationClip[] animations) {
			return ImportGLB(bytes, importSettings, out animations);
		}

		public static void LoadFromFileAsync(string filepath, ImportSettings importSettings, Action<GameObject, AnimationClip[]> onFinished, Action<float> onProgress = null) {
			string extension = Path.GetExtension(filepath).ToLower();
			if (extension == ".glb") ImportGLBAsync(filepath, importSettings, onFinished, onProgress);
			else if (extension == ".gltf") ImportGLTFAsync(filepath, importSettings, onFinished, onProgress);
			else {
				UnityEngine.Debug.Log("Extension '" + extension + "' not recognized in " + filepath);
				onFinished(null, null);
			}
		}

#region GLB
		// hkyoo
		private static GLTFObject ImportGLB(string filepath, out long binChunk)
		{
			FileStream stream = File.OpenRead(filepath);
			string json = GetGLBJson(stream, out binChunk);
			GLTFObject gltfObject = JsonConvert.DeserializeObject<GLTFObject>(json);

			CheckExtensions(gltfObject);

			return gltfObject;
		}

		private static GameObject ImportGLB(string filepath, ImportSettings importSettings, out AnimationClip[] animations) {
			FileStream stream = File.OpenRead(filepath);
			long binChunkStart;
			string json = GetGLBJson(stream, out binChunkStart);
			GLTFObject gltfObject = JsonConvert.DeserializeObject<GLTFObject>(json);
			return gltfObject.LoadInternal(filepath, null, binChunkStart, importSettings, out animations);
		}

		private static GameObject ImportGLB(byte[] bytes, ImportSettings importSettings, out AnimationClip[] animations) {
			Stream stream = new MemoryStream(bytes);
			long binChunkStart;
			string json = GetGLBJson(stream, out binChunkStart);
			GLTFObject gltfObject = JsonConvert.DeserializeObject<GLTFObject>(json);
			return gltfObject.LoadInternal(null, bytes, binChunkStart, importSettings, out animations);
		}

		public static void ImportGLBAsync(string filepath, ImportSettings importSettings, Action<GameObject, AnimationClip[]> onFinished, Action<float> onProgress = null) {
			FileStream stream = File.OpenRead(filepath);
			long binChunkStart;
			string json = GetGLBJson(stream, out binChunkStart);
			LoadAsync(json, filepath, null, binChunkStart, importSettings, onFinished, onProgress).RunCoroutine();
		}

		public static void ImportGLBAsync(byte[] bytes, ImportSettings importSettings, Action<GameObject, AnimationClip[]> onFinished, Action<float> onProgress = null) {
			Stream stream = new MemoryStream(bytes);
			long binChunkStart;
			string json = GetGLBJson(stream, out binChunkStart);
			LoadAsync(json, null, bytes, binChunkStart, importSettings, onFinished, onProgress).RunCoroutine();
		}

		private static string GetGLBJson(Stream stream, out long binChunkStart) {
			byte[] buffer = new byte[12];
			stream.Read(buffer, 0, 12);
			// 12 byte header
			// 0-4  - magic = "glTF"
			// 4-8  - version = 2
			// 8-12 - length = total length of glb, including Header and all Chunks, in bytes.
			string magic = Encoding.Default.GetString(buffer, 0, 4);
			if (magic != "glTF") {
				UnityEngine.Debug.LogWarning("Input does not look like a .glb file");
				binChunkStart = 0;
				return null;
			}
			uint version = System.BitConverter.ToUInt32(buffer, 4);
			if (version != 2) {
				UnityEngine.Debug.LogWarning("Importer does not support gltf version " + version);
				binChunkStart = 0;
				return null;
			}
			// What do we even need the length for.
			//uint length = System.BitConverter.ToUInt32(bytes, 8);

			// Chunk 0 (json)
			// 0-4  - chunkLength = total length of the chunkData
			// 4-8  - chunkType = "JSON"
			// 8-[chunkLength+8] - chunkData = json data.
			stream.Read(buffer, 0, 8);
			uint chunkLength = System.BitConverter.ToUInt32(buffer, 0);
			TextReader reader = new StreamReader(stream);
			char[] jsonChars = new char[chunkLength];
			reader.Read(jsonChars, 0, (int) chunkLength);
			string json = new string(jsonChars);

			// Chunk
			binChunkStart = chunkLength + 20;
			stream.Close();

			// Return json
			return json;
		}
#endregion

		private static GameObject ImportGLTF(string filepath, ImportSettings importSettings, out AnimationClip[] animations) {
			string json = File.ReadAllText(filepath);

			// Parse json
			GLTFObject gltfObject = JsonConvert.DeserializeObject<GLTFObject>(json);
			return gltfObject.LoadInternal(filepath, null, 0, importSettings, out animations);
		}

		public static void ImportGLTFAsync(string filepath, ImportSettings importSettings, Action<GameObject, AnimationClip[]> onFinished, Action<float> onProgress = null) {
			string json = File.ReadAllText(filepath);

			// Parse json
			LoadAsync(json, filepath, null, 0, importSettings, onFinished, onProgress).RunCoroutine();
		}

		public abstract class ImportTask<TReturn> : ImportTask {
			public TReturn Result;

			/// <summary> Constructor. Sets waitFor which ensures ImportTasks are completed before running. </summary>
			public ImportTask(params ImportTask[] waitFor) : base(waitFor) { }

			/// <summary> Runs task followed by OnCompleted </summary>
			public TReturn RunSynchronously() {
				task.RunSynchronously();
				IEnumerator en = OnCoroutine();
				while (en.MoveNext()) { };
				return Result;
			}
		}

		public abstract class ImportTask {
			public Task task;
			public readonly ImportTask[] waitFor;
			public bool IsReady { get { return waitFor.All(x => x.IsCompleted); } }
			public bool IsCompleted { get; protected set; }

			/// <summary> Constructor. Sets waitFor which ensures ImportTasks are completed before running. </summary>
			public ImportTask(params ImportTask[] waitFor) {
				IsCompleted = false;
				this.waitFor = waitFor;
			}

			public virtual IEnumerator OnCoroutine(Action<float> onProgress = null) {
				IsCompleted = true;
				yield break;
			}
		}

		#region Sync
		// hkyoo
		public static GameObject LoadInternal(this GLTFObject gltfObject, string filepath, int index, byte[] bytefile, long binChunkStart, ImportSettings importSettings, out AnimationClip[] animations)
		{
			// directory root is sometimes used for loading buffers from containing file, or local images
			string directoryRoot = filepath != null ? Directory.GetParent(filepath).ToString() + "/" : null;

			importSettings.shaderOverrides.CacheDefaultShaders();

			//// for debug
			//Debug.Log(gltfObject.nodes.ToString() + " " + gltfObject.nodes.Count); // 151 
			//Debug.Log(gltfObject.meshes.ToString() + " " + gltfObject.meshes.Count); // 151
			////Debug.Log(gltfObject.animations.ToString() + " " + gltfObject.animations.Count); // null
			//Debug.Log(gltfObject.buffers.ToString() + " " + gltfObject.buffers.Count); // 1
			//Debug.Log(gltfObject.bufferViews.ToString() + " " + gltfObject.bufferViews.Count); // 755
			//Debug.Log(gltfObject.accessors.ToString() + " " + gltfObject.accessors.Count); // 604
			////Debug.Log(gltfObject.skins.ToString() + " " + gltfObject.skins.Count); // null
			//Debug.Log(gltfObject.textures.ToString() + " " + gltfObject.textures.Count); // 151
			//Debug.Log(gltfObject.images.ToString() + " " + gltfObject.images.Count); // 151
			//Debug.Log(gltfObject.materials.ToString() + " " + gltfObject.materials.Count); // 151
			////Debug.Log(gltfObject.cameras.ToString() + " " + gltfObject.cameras.Count); // null
			///

			Stopwatch sw = new Stopwatch();
				
	
			// image/material/mesh

			sw.Start();
			// Import tasks synchronously
			GLTFBuffer.ImportTask bufferTask = new GLTFBuffer.ImportTask(gltfObject.buffers, filepath, bytefile, binChunkStart);
			bufferTask.RunSynchronously();
			sw.Stop();
			UnityEngine.Debug.Log("bufferTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();

			// TODO?
			GLTFBufferView.ImportTask bufferViewTask = new GLTFBufferView.ImportTask(gltfObject.bufferViews, bufferTask);
			bufferViewTask.RunSynchronously();

			sw.Stop();
			UnityEngine.Debug.Log("bufferViewTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();


			GLTFAccessor.ImportTask accessorTask = new GLTFAccessor.ImportTask(gltfObject.accessors, bufferViewTask);
			accessorTask.RunSynchronously();

			sw.Stop();
			UnityEngine.Debug.Log("accessorTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();

			GLTFImage.ImportTask imageTask = new GLTFImage.ImportTask(gltfObject.images[index], directoryRoot, bufferViewTask);
			//GLTFImage.ImportTask imageTask = new GLTFImage.ImportTask(gltfObject.images, directoryRoot, bufferViewTask);
			imageTask.RunSynchronously();

			sw.Stop();
			UnityEngine.Debug.Log("imageTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();

			GLTFTexture.ImportTask textureTask = new GLTFTexture.ImportTask(gltfObject.textures[index], imageTask);
			//GLTFTexture.ImportTask textureTask = new GLTFTexture.ImportTask(gltfObject.textures, imageTask);
			textureTask.RunSynchronously();

			UnityEngine.Debug.Log("textureTask.Result[0] " + textureTask.Result.Length + " " + textureTask.Result[0]);

			sw.Stop();
			UnityEngine.Debug.Log("textureTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();

			// TODO : shader setting
			GLTFMaterial.ImportTask materialTask = new GLTFMaterial.ImportTask(gltfObject.materials[index], textureTask, importSettings);
			//GLTFMaterial.ImportTask materialTask = new GLTFMaterial.ImportTask(gltfObject.materials, textureTask, importSettings);

			materialTask.RunSynchronously();

			sw.Stop();
			UnityEngine.Debug.Log("materialTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();

			// TODO : 
			GLTFMesh.ImportTask meshTask = new GLTFMesh.ImportTask(gltfObject.meshes[index], accessorTask, bufferViewTask, materialTask, importSettings);
			//GLTFMesh.ImportTask meshTask = new GLTFMesh.ImportTask(gltfObject.meshes, accessorTask, bufferViewTask, materialTask, importSettings);

			meshTask.RunSynchronously();

			sw.Stop();
			UnityEngine.Debug.Log("meshTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();

			GLTFSkin.ImportTask skinTask = new GLTFSkin.ImportTask(gltfObject.skins, accessorTask);
			skinTask.RunSynchronously();

			sw.Stop();
			UnityEngine.Debug.Log("skinTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();

			sw.Start();


			GLTFNode.ImportTask nodeTask = new GLTFNode.ImportTask(gltfObject.nodes[index], meshTask, skinTask, gltfObject.cameras);
			//GLTFNode.ImportTask nodeTask = new GLTFNode.ImportTask(gltfObject.nodes, meshTask, skinTask, gltfObject.cameras);
			nodeTask.RunSynchronously();

			sw.Stop();
			UnityEngine.Debug.Log("nodeTask : " + sw.ElapsedMilliseconds.ToString() + "ms");
			sw.Reset();



			GLTFAnimation.ImportResult[] animationResult = gltfObject.animations.Import(accessorTask.Result, nodeTask.Result, importSettings);
			if (animationResult != null) animations = animationResult.Select(x => x.clip).ToArray();
			else animations = new AnimationClip[0];

			foreach (var item in bufferTask.Result)
			{
				item.Dispose();
			}

			return nodeTask.Result.GetRoot();
		}

		private static GameObject LoadInternal(this GLTFObject gltfObject, string filepath, byte[] bytefile, long binChunkStart, ImportSettings importSettings, out AnimationClip[] animations) {
			CheckExtensions(gltfObject);

			// directory root is sometimes used for loading buffers from containing file, or local images
			string directoryRoot = filepath != null ? Directory.GetParent(filepath).ToString() + "/" : null;

			importSettings.shaderOverrides.CacheDefaultShaders();

			// Import tasks synchronously
			GLTFBuffer.ImportTask bufferTask = new GLTFBuffer.ImportTask(gltfObject.buffers, filepath, bytefile, binChunkStart);
			bufferTask.RunSynchronously();
			GLTFBufferView.ImportTask bufferViewTask = new GLTFBufferView.ImportTask(gltfObject.bufferViews, bufferTask);
			bufferViewTask.RunSynchronously();
			GLTFAccessor.ImportTask accessorTask = new GLTFAccessor.ImportTask(gltfObject.accessors, bufferViewTask);
			accessorTask.RunSynchronously();
			GLTFImage.ImportTask imageTask = new GLTFImage.ImportTask(gltfObject.images, directoryRoot, bufferViewTask);
			imageTask.RunSynchronously();
			GLTFTexture.ImportTask textureTask = new GLTFTexture.ImportTask(gltfObject.textures, imageTask);
			textureTask.RunSynchronously();
			GLTFMaterial.ImportTask materialTask = new GLTFMaterial.ImportTask(gltfObject.materials, textureTask, importSettings);
			materialTask.RunSynchronously();
			GLTFMesh.ImportTask meshTask = new GLTFMesh.ImportTask(gltfObject.meshes, accessorTask, bufferViewTask, materialTask, importSettings);
			meshTask.RunSynchronously();
			GLTFSkin.ImportTask skinTask = new GLTFSkin.ImportTask(gltfObject.skins, accessorTask);
			skinTask.RunSynchronously();
			GLTFNode.ImportTask nodeTask = new GLTFNode.ImportTask(gltfObject.nodes, meshTask, skinTask, gltfObject.cameras);
			nodeTask.RunSynchronously();
			GLTFAnimation.ImportResult[] animationResult = gltfObject.animations.Import(accessorTask.Result, nodeTask.Result, importSettings);
			if (animationResult != null) animations = animationResult.Select(x => x.clip).ToArray();
			else animations = new AnimationClip[0];

			foreach (var item in bufferTask.Result) {
				item.Dispose();
			}

			return nodeTask.Result.GetRoot();
		}
#endregion

#region Async
		private static IEnumerator LoadAsync(string json, string filepath, byte[] bytefile, long binChunkStart, ImportSettings importSettings, Action<GameObject, AnimationClip[]> onFinished, Action<float> onProgress = null) {
			// Threaded deserialization
			Task<GLTFObject> deserializeTask = new Task<GLTFObject>(() => JsonConvert.DeserializeObject<GLTFObject>(json));
			deserializeTask.Start();
			while (!deserializeTask.IsCompleted) yield return null;
			GLTFObject gltfObject = deserializeTask.Result;
			CheckExtensions(gltfObject);

			// directory root is sometimes used for loading buffers from containing file, or local images
			string directoryRoot = filepath != null ? Directory.GetParent(filepath).ToString() + "/" : null;

			importSettings.shaderOverrides.CacheDefaultShaders();

			// Setup import tasks
			List<ImportTask> importTasks = new List<ImportTask>();

			GLTFBuffer.ImportTask bufferTask = new GLTFBuffer.ImportTask(gltfObject.buffers, filepath, bytefile, binChunkStart);
			importTasks.Add(bufferTask);
			GLTFBufferView.ImportTask bufferViewTask = new GLTFBufferView.ImportTask(gltfObject.bufferViews, bufferTask);
			importTasks.Add(bufferViewTask);
			GLTFAccessor.ImportTask accessorTask = new GLTFAccessor.ImportTask(gltfObject.accessors, bufferViewTask);
			importTasks.Add(accessorTask);
			GLTFImage.ImportTask imageTask = new GLTFImage.ImportTask(gltfObject.images, directoryRoot, bufferViewTask);
			importTasks.Add(imageTask);
			GLTFTexture.ImportTask textureTask = new GLTFTexture.ImportTask(gltfObject.textures, imageTask);
			importTasks.Add(textureTask);
			GLTFMaterial.ImportTask materialTask = new GLTFMaterial.ImportTask(gltfObject.materials, textureTask, importSettings);
			importTasks.Add(materialTask);
			GLTFMesh.ImportTask meshTask = new GLTFMesh.ImportTask(gltfObject.meshes, accessorTask, bufferViewTask, materialTask, importSettings);
			importTasks.Add(meshTask);
			GLTFSkin.ImportTask skinTask = new GLTFSkin.ImportTask(gltfObject.skins, accessorTask);
			importTasks.Add(skinTask);
			GLTFNode.ImportTask nodeTask = new GLTFNode.ImportTask(gltfObject.nodes, meshTask, skinTask, gltfObject.cameras);
			importTasks.Add(nodeTask);

			// Ignite
			for (int i = 0; i < importTasks.Count; i++) {
				TaskSupervisor(importTasks[i], onProgress).RunCoroutine();
			}

			// Wait for all tasks to finish
			while (!importTasks.All(x => x.IsCompleted)) yield return null;

			// Fire onFinished when all tasks have completed
			GameObject root = nodeTask.Result.GetRoot();
			GLTFAnimation.ImportResult[] animationResult = gltfObject.animations.Import(accessorTask.Result, nodeTask.Result, importSettings);
			AnimationClip[] animations = new AnimationClip[0];
			if (animationResult != null) animations = animationResult.Select(x => x.clip).ToArray();
			if (onFinished != null) onFinished(nodeTask.Result.GetRoot(), animations);

			// Close file streams
			foreach (var item in bufferTask.Result) {
				item.Dispose();
			}
		}

		/// <summary> Keeps track of which threads to start when </summary>
		private static IEnumerator TaskSupervisor(ImportTask importTask, Action<float> onProgress = null) {
			// Wait for required results to complete before starting
			while (!importTask.IsReady) yield return null;
			// Start threaded task
			importTask.task.Start();
			// Wait for task to complete
			while (!importTask.task.IsCompleted) yield return null;
			// Run additional unity code on main thread
			importTask.OnCoroutine(onProgress).RunCoroutine();
			//Wait for additional coroutines to complete
			while (!importTask.IsCompleted) { yield return null; }
		}
#endregion

		private static void CheckExtensions(GLTFObject gLTFObject) {
			if (gLTFObject.extensionsRequired != null) {
				for (int i = 0; i < gLTFObject.extensionsRequired.Count; i++) {
					switch (gLTFObject.extensionsRequired[i]) {
						case "KHR_materials_pbrSpecularGlossiness":
							break;
						case "KHR_draco_mesh_compression":
							break;
						default:
							UnityEngine.Debug.LogWarning($"GLTFUtility: Required extension '{gLTFObject.extensionsRequired[i]}' not supported. Import process will proceed but results may vary.");
							break;
					}
				}
			}
		}
	}
}