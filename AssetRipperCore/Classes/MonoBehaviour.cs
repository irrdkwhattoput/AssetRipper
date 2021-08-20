using AssetRipper.Core.Project;
using AssetRipper.Core.Layout.Classes;
using AssetRipper.Core.Logging;
using AssetRipper.Core.Parser.Asset;
using AssetRipper.Core.Classes.Misc;
using AssetRipper.Core.Classes.Object;
using AssetRipper.Core.Parser.Files.SerializedFiles.Parser;
using AssetRipper.Core.IO.Asset;
using AssetRipper.Core.Structure.Assembly.Serializable;
using AssetRipper.Core.YAML;
using System.Collections.Generic;
using System.IO;

namespace AssetRipper.Core.Classes
{
	public sealed class MonoBehaviour : Behaviour
	{
		public MonoBehaviour(AssetInfo assetInfo) : base(assetInfo) { }

		public override void Read(AssetReader reader)
		{
			long position = reader.BaseStream.Position;
			base.Read(reader);

			MonoBehaviourLayout layout = reader.Layout.MonoBehaviour;
			if (layout.HasEditorHideFlags)
			{
				EditorHideFlags = (HideFlags)reader.ReadUInt32();
			}
			if (layout.HasGeneratorAsset)
			{
				GeneratorAsset.Read(reader);
			}

			Script.Read(reader);
			Name = reader.ReadString();

			if (layout.HasEditorClassIdentifier)
			{
				EditorClassIdentifier = reader.ReadString();
			}

			if (!ReadStructure(reader))
			{
				ObjectInfo info = File.GetAssetEntry(PathID);
				reader.BaseStream.Position = position + info.ByteSize;
			}
		}

		public override void Write(AssetWriter writer)
		{
			base.Write(writer);

			MonoBehaviourLayout layout = writer.Layout.MonoBehaviour;
			if (layout.HasEditorHideFlags)
			{
				writer.Write((uint)EditorHideFlags);
			}
			if (layout.HasGeneratorAsset)
			{
				GeneratorAsset.Write(writer);
			}

			Script.Write(writer);
			writer.Write(Name);

			if (layout.HasEditorClassIdentifier)
			{
				writer.Write(EditorClassIdentifier);
			}

			if (Structure != null)
			{
				Structure.Write(writer);
			}
		}

		public override IEnumerable<PPtr<Object.Object>> FetchDependencies(DependencyContext context)
		{
			foreach (PPtr<Object.Object> asset in base.FetchDependencies(context))
			{
				yield return asset;
			}

			MonoBehaviourLayout layout = context.Layout.MonoBehaviour;
			yield return context.FetchDependency(GeneratorAsset, layout.GeneratorAssetName);
			yield return context.FetchDependency(Script, layout.ScriptName);

			if (Structure != null)
			{
				foreach (PPtr<Object.Object> asset in context.FetchDependencies(Structure, Structure.Type.Name))
				{
					yield return asset;
				}
			}
		}

		public override string ToString()
		{
			return $"{Name}({nameof(MonoBehaviour)})";
		}

		protected override YAMLMappingNode ExportYAMLRoot(IExportContainer container)
		{
			YAMLMappingNode node = base.ExportYAMLRoot(container);
			MonoBehaviourLayout layout = container.ExportLayout.MonoBehaviour;
			node.Add(layout.EditorHideFlagsName, (uint)GetEditorHideFlags(container));
			if (layout.HasGeneratorAsset)
			{
				node.Add(layout.GeneratorAssetName, GetGeneratorAsset(container).ExportYAML(container));
			}
			node.Add(layout.ScriptName, Script.ExportYAML(container));
			node.Add(layout.NameName, Name);
			node.Add(layout.EditorClassIdentifierName, GetEditorClassIdentifier(container));
			if (Structure != null)
			{
				YAMLMappingNode structureNode = (YAMLMappingNode)Structure.ExportYAML(container);
				node.Append(structureNode);
			}
			return node;
		}

		private HideFlags GetEditorHideFlags(IExportContainer container)
		{
			if (container.Layout.MonoBehaviour.HasEditorHideFlags)
			{
				return EditorHideFlags;
			}
			return HideFlags.None;
		}
		private PPtr<Object.Object> GetGeneratorAsset(IExportContainer container)
		{
			if (container.Layout.MonoBehaviour.HasGeneratorAsset)
			{
				return GeneratorAsset;
			}
			return default;
		}
		private string GetEditorClassIdentifier(IExportContainer container)
		{
			if (container.Layout.MonoBehaviour.HasEditorClassIdentifier)
			{
				return EditorClassIdentifier;
			}
			return string.Empty;
		}

		/// <summary>Reads the structure with an AssetReader</summary>
		/// <returns>
		/// Returns true if the position does not need reset.<br/>
		/// Returns false if the position does need reset. 
		/// </returns>
		private bool ReadStructure(AssetReader reader)
		{
			if (!File.Collection.AssemblyManager.IsSet)
			{
				return false;
			}

			MonoScript script = Script.FindAsset(File);
			if (script == null)
			{
				return false;
			}

			SerializableType behaviourType = script.GetBehaviourType();
			if (behaviourType == null)
			{
				Logger.Log(LogType.Warning, LogCategory.Import, $"Unable to read {ValidName}, because valid definition for script {script.ValidName} wasn't found");
				return false;
			}

			Structure = behaviourType.CreateSerializableStructure();
#if !DEBUG
			try
#endif
			{
				Structure.Read(reader);
			}
#if !DEBUG
			catch(System.Exception ex)
			{
				Structure = null;
				Logger.Log(LogType.Error, LogCategory.Import, $"Unable to read {ValidName}, because script layout {script.ValidName} mismatch binary content");
				Logger.Log(LogType.Debug, LogCategory.Import, $"Stack trace: {ex.ToString()}");
			}
			return false;//In a Release Build, always recalculate the position.
#else
			return true;//In a Debug Build, let the position stay as is.
#endif
		}

		public override string ExportPath => Path.Combine(AssetsKeyword, "ScriptableObject");
		public override string ExportExtension => AssetExtension;

		public string ValidName => Name.Length == 0 ? nameof(MonoBehaviour) : Name;
		/// <summary>
		/// Whether this MonoBeh belongs to scene/prefab hierarchy or not
		/// </summary>
		// TODO: find out why GameObject may has value like PPtr(0, 894) but such game object doesn't exists
		public bool IsSceneObject => !GameObject.IsNull;
		public bool IsScriptableObject => Name.Length > 0;

		/// <summary> Editor Only </summary>
		public HideFlags EditorHideFlags { get; set; }
		public string Name { get; set; }
		public SerializableStructure Structure { get; set; }
		/// <summary> Editor Only </summary>
		public string EditorClassIdentifier { get; set; }

		/// <summary> Editor Only </summary>
		public PPtr<Object.Object> GeneratorAsset { get; set; }
		public PPtr<MonoScript> Script { get; set; }
	}
}
