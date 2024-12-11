using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Plugins.Core.Assets;
using Warudo.Plugins.Core.Assets.Character;
using System.Collections.Generic;
using DG.Tweening;

namespace Warudo.Plugins.Core.Nodes
{
    [NodeType(Id = "0326.CharacterMeshObjectItemListPlugin", Title = "Bone Parent Constraint", Category = "SamyLabs")]
    public class BoneParentConstraintNode : Node 
    {
        [DataInput]
        [Label("Source Character")]
        public CharacterAsset SourceCharacter;

        [DataInput]
        [Label("Target Asset")]
        public GameObjectAsset TargetAsset;

        [DataInput]
        [Label("Sync Bones")]
        public bool SyncBones = true;

        [DataInput]
        [Label("Sync Bone Scale")]
        public bool SyncBoneScale = true;

        [DataInput]
        [Label("Sync Asset Scale")]
        public bool SyncAssetScale = true;

        private Animator _targetAnimator;
        private bool _initialized = false;

        protected override void OnCreate()
        {
            base.OnCreate();

            Watch<CharacterAsset>(nameof(SourceCharacter), (from, to) =>
            {
                _initialized = false;
                InitializeAnimator();
            });

            Watch<GameObjectAsset>(nameof(TargetAsset), (from, to) =>
            {
                _initialized = false;
                InitializeAnimator();
            });

            WatchAsset(nameof(SourceCharacter), () =>
            {
                _initialized = false;
                InitializeAnimator();
            });

            WatchAsset(nameof(TargetAsset), () =>
            {
                _initialized = false;
                InitializeAnimator();
            });
        }

        private void InitializeAnimator()
        {
            if (_initialized) return;

            if (TargetAsset?.GameObject == null) return;

            _targetAnimator = TargetAsset.GameObject.GetComponent<Animator>();
            if (_targetAnimator == null) return;

            // 애니메이터 설정 최적화
            _targetAnimator.updateMode = AnimatorUpdateMode.AnimatePhysics;
            _targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            
            _initialized = true;
        }

        private void Execute()
        {
            if (SourceCharacter == null || TargetAsset == null) return;

            var sourceAnimator = SourceCharacter.Animator;
            if (sourceAnimator == null) return;

            InitializeAnimator();
            if (_targetAnimator == null) return;

            // 에셋 전체 스케일 동기화
            if (SyncAssetScale)
            {
                TargetAsset.GameObject.transform.localScale = SourceCharacter.GameObject.transform.localScale;
            }

            // 본 동기화가 비활성화되어 있다면 여기서 종료
            if (!SyncBones) return;

            // 모든 HumanBodyBones에 대해 처리
            foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;

                Transform sourceBone = sourceAnimator.GetBoneTransform(bone);
                Transform targetBone = _targetAnimator.GetBoneTransform(bone);

                if (sourceBone == null || targetBone == null) continue;

                // Transform 즉시 적용
                targetBone.position = sourceBone.position;
                targetBone.rotation = sourceBone.rotation;
                
                // 본 스케일 동기화 (옵션)
                if (SyncBoneScale)
                {
                    targetBone.localScale = sourceBone.localScale;
                }
            }
        }

        public override void OnUpdate()
        {
            // Update에서는 초기화만 수행
            if (!_initialized)
            {
                InitializeAnimator();
            }
        }

        public override void OnLateUpdate()
        {
            base.OnLateUpdate();
            Execute(); // LateUpdate에서 Transform 적용
        }
    }
}
