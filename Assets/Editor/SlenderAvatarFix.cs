using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sokophobia.Editor
{
    public static class SlenderAvatarFix
    {
        private static readonly KeyValuePair<string, string>[] Map =
        {
            new("Hips", "hips"),
            new("Spine", "spine"),
            new("Chest", "chest"),
            new("UpperChest", "chest1"),
            new("Neck", "neck"),
            new("Head", "head"),
            new("LeftEye", "eye.L"),
            new("RightEye", "eye.R"),
            new("LeftShoulder", "shoulder.L"),
            new("RightShoulder", "shoulder.R"),
            new("LeftUpperArm", "upper_arm.L"),
            new("LeftLowerArm", "forearm.L"),
            new("LeftHand", "hand.L"),
            new("RightUpperArm", "upper_arm.R"),
            new("RightLowerArm", "forearm.R"),
            new("RightHand", "hand.R"),
            new("LeftUpperLeg", "thigh.L"),
            new("LeftLowerLeg", "shin.L"),
            new("LeftFoot", "foot.L"),
            new("LeftToes", "toe.L"),
            new("RightUpperLeg", "thigh.R"),
            new("RightLowerLeg", "shin.R"),
            new("RightFoot", "foot.R"),
            new("RightToes", "toe.R"),
        };

        private const string AvatarPath = "Assets/#NVJOB Slender/Slender/Mesh/SlenderHuman.avatar";

        [MenuItem("Sokophobia/Fix Slender Avatar")]
        private static void FixFromMenu() => Fix();

        [MenuItem("Sokophobia/Rebind Slender Bindposes")]
        private static void RebindFromMenu() => RebindBindposes();

        private const string ReboundMeshPath = "Assets/#NVJOB Slender/Slender/Mesh/SlenderRebound.asset";

        public static void RebindBindposes()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SlenderAvatarFix] Exit Play mode first.");
                return;
            }

            var bot = GameObject.Find("Sokobot");
            if (bot == null)
            {
                Debug.LogError("[SlenderAvatarFix] No 'Sokobot' in the open scene.");
                return;
            }
            var smr = bot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null)
            {
                Debug.LogError("[SlenderAvatarFix] No SkinnedMeshRenderer under Sokobot.");
                return;
            }
            var bones = smr.bones;
            if (bones == null || bones.Length == 0)
            {
                Debug.LogError("[SlenderAvatarFix] Renderer has no bones array.");
                return;
            }
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                {
                    Debug.LogError($"[SlenderAvatarFix] bones[{i}] is null, aborting.");
                    return;
                }
            }

            Matrix4x4 meshRootL2W = smr.transform.localToWorldMatrix;
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bind[i] = bones[i].worldToLocalMatrix * meshRootL2W;

            Mesh copy = Object.Instantiate(smr.sharedMesh);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(ReboundMeshPath);
            copy.bindposes = bind;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(ReboundMeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                Object.DestroyImmediate(copy);
                copy = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(copy, ReboundMeshPath);
            }

            smr.sharedMesh = copy;
            EditorUtility.SetDirty(smr);
            EditorSceneManager.MarkSceneDirty(smr.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SlenderAvatarFix] Rebound {bones.Length} bones -> {ReboundMeshPath}");
        }

        public static void Fix()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SlenderAvatarFix] Exit Play mode first.");
                return;
            }

            var bot = GameObject.Find("Sokobot");
            if (bot == null)
            {
                Debug.LogError("[SlenderAvatarFix] No 'Sokobot' in the open scene.");
                return;
            }

            Transform hips = null;
            foreach (var t in bot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "hips") { hips = t; break; }
            }
            if (hips == null)
            {
                Debug.LogError("[SlenderAvatarFix] No 'hips' bone under Sokobot (is SlenderBody parented?).");
                return;
            }

            Transform scaledContainer = hips.parent != null ? hips.parent : hips;
            Transform root = scaledContainer.parent != null ? scaledContainer.parent : scaledContainer;
            var bones = new List<Transform>();
            CollectBones(root, bones);

            var skeleton = new SkeletonBone[bones.Count];
            var byName = new Dictionary<string, Transform>();
            for (int i = 0; i < bones.Count; i++)
            {
                skeleton[i] = new SkeletonBone
                {
                    name = bones[i].name,
                    position = bones[i].localPosition,
                    rotation = bones[i].localRotation,
                    scale = bones[i].localScale,
                };
                if (!byName.ContainsKey(bones[i].name))
                    byName.Add(bones[i].name, bones[i]);
            }

            var human = new List<HumanBone>();
            foreach (var kv in Map)
            {
                if (!byName.ContainsKey(kv.Value))
                {
                    Debug.LogWarning($"[SlenderAvatarFix] Bone '{kv.Value}' not found, skipping {kv.Key}.");
                    continue;
                }
                human.Add(new HumanBone
                {
                    humanName = kv.Key,
                    boneName = kv.Value,
                    limit = new HumanLimit { useDefaultValues = true },
                });
            }

            var desc = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton,
                armStretch = 0.05f,
                legStretch = 0.05f,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root.gameObject, desc);
            if (avatar == null || !avatar.isValid)
            {
                Debug.LogError("[SlenderAvatarFix] AvatarBuilder produced an invalid avatar. " +
                               "Check that Hips/Legs/Spine/Arms all mapped.");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(avatar, existing);
                avatar = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(avatar, AvatarPath);
            }

            var anim = bot.GetComponentInChildren<Animator>();
            if (anim == null)
            {
                Debug.LogError("[SlenderAvatarFix] No Animator under Sokobot.");
                return;
            }
            anim.avatar = avatar;
            EditorUtility.SetDirty(anim);
            EditorSceneManager.MarkSceneDirty(anim.gameObject.scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SlenderAvatarFix] OK valid={avatar.isValid} human={avatar.isHuman} " +
                      $"bones={skeleton.Length} mapped={human.Count} -> {AvatarPath}");
        }

        private static void CollectBones(Transform t, List<Transform> outBones)
        {
            outBones.Add(t);
            for (int i = 0; i < t.childCount; i++)
                CollectBones(t.GetChild(i), outBones);
        }
    }
}
