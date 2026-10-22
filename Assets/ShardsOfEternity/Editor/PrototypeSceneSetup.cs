using System.IO;
using System.Linq;
using ShardsOfEternity.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShardsOfEternity.EditorTools
{
    /// <summary>
    /// Creates the prototype scene the first time the project is opened, so a fresh clone is one click from Play.
    /// Also available from the menu: Shards of Eternity > Open Prototype Scene.
    /// </summary>
    [InitializeOnLoad]
    static class PrototypeSceneSetup
    {
        const string ScenePath = "Assets/ShardsOfEternity/Scenes/Prototype.unity";
        const string SessionKey = "ShardsOfEternity.PrototypeSceneChecked";

        static PrototypeSceneSetup()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode) OpenPrototypeScene();
            };
        }

        [MenuItem("Shards of Eternity/Open Prototype Scene", priority = 0)]
        public static void OpenPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Shards of Eternity").AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log($"Shards of Eternity: created {ScenePath}. Press Play to start the combat prototype.");
        }
    }
}
