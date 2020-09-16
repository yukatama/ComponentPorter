using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Yukatamax
{

    public class CompornentPorterWindow : EditorWindow
    {
        private const string _menuItemTitle = "Yukatamax/Component Porter... ";
        private const string _windowTitle = "Component Porter";
        private const string _labelGameObjectSettings = "GameObject Settings";
        private const string _labelSourceGameObject = "Source GameObject";
        private const string _labelDestinationGameObject = "Destination GameObject";
        private const string _labelTargetComponentSettings = "Target Component Settings";
        private const string _labelVrcAvatarDescriptor = "VRC_Avatar Descriptor";
        private const string _labelAnimator = "Animator";
        private const string _labelDynamicBone = "Dynamic Bone";
        private const string _labelDynamicBoneCollider = "Dynamic Bone Collider";
        private const string _labelApply = "Apply";

        private GameObject _sourceGameObject;
        private GameObject _destinationGameObject;
        private bool _vrcAvatarDescriptorEnabled;
        private bool _animatorEnabled;
        private bool _dynamicBoneEnabled;
        private bool _dynamicBoneColliderEnabled;

        [MenuItem(_menuItemTitle)]
        private static void Init()
        {
            CompornentPorterWindow window = (CompornentPorterWindow)EditorWindow.GetWindow(typeof(CompornentPorterWindow), false, _windowTitle);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label(_labelGameObjectSettings, EditorStyles.boldLabel);

            _sourceGameObject = EditorGUILayout.ObjectField(_labelSourceGameObject, _sourceGameObject, typeof(GameObject), true) as GameObject;
            _destinationGameObject = EditorGUILayout.ObjectField(_labelDestinationGameObject, _destinationGameObject, typeof(GameObject), true) as GameObject;

            EditorGUILayout.Space();

            GUILayout.Label(_labelTargetComponentSettings, EditorStyles.boldLabel);

            _vrcAvatarDescriptorEnabled = EditorGUILayout.BeginToggleGroup(_labelVrcAvatarDescriptor, _vrcAvatarDescriptorEnabled);
            EditorGUILayout.EndToggleGroup();
            _animatorEnabled = EditorGUILayout.BeginToggleGroup(_labelAnimator, _animatorEnabled);
            EditorGUILayout.EndToggleGroup();
            _dynamicBoneEnabled = EditorGUILayout.BeginToggleGroup(_labelDynamicBone, _dynamicBoneEnabled);
            EditorGUILayout.EndToggleGroup();
            _dynamicBoneColliderEnabled = EditorGUILayout.BeginToggleGroup(_labelDynamicBoneCollider, _dynamicBoneColliderEnabled);
            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.Space();

            if (GUILayout.Button(_labelApply))
            {
                Debug.Log("Applying...");

                if (_vrcAvatarDescriptorEnabled)
                    PortComponent("(VRCSDK2.VRC_AvatarDescriptor)");
                if (_animatorEnabled)
                    PortComponent("(UnityEngine.Animator)");
                if (_dynamicBoneEnabled)
                    PortComponent("(DynamicBone)");
                if (_dynamicBoneColliderEnabled)
                    PortComponent("(DynamicBoneCollider)");

                Debug.Log("Done");
            }
        }

        private void PortComponent(string key)
        {
            var i = new ComponentEnumerator(_sourceGameObject, _destinationGameObject, key).GetEnumerator();
            while (i.MoveNext())
                CloneComponent(i.Current.component, i.Current.destination, _destinationGameObject);
        }

        private void CloneComponent(Component original, GameObject destination, GameObject destinationRoot)
        {
            Debug.Log("porting " + original.ToString());
            ComponentUtility.CopyComponent(original);
            var host = new GameObject();
            ComponentUtility.PasteComponentAsNew(host);

            var type = original.GetType();
            var clone = host.GetComponent(type);

            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var member in members)
            {
                if (member.MemberType != MemberTypes.Field)
                    continue;
                var field = type.GetField(member.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field.FieldType == typeof(SkinnedMeshRenderer) || field.FieldType == typeof(Transform))
                {
                    Component value = (Component)field.GetValue(clone);
                    if (!value)
                        continue;
                    var found = FindComponent(destinationRoot.transform, field.FieldType, value.name);
                    if (found)
                    {
                        Debug.Log("replacing (" + field.FieldType.Name + ")" + field.Name + " '" + value.name + "' in " + original.ToString());
                        field.SetValue(clone, found);
                    }
                    else
                    {
                        Debug.LogError("Fail to replace (" + field.FieldType.Name + ")" + field.Name + " '" + value.name + "' in " + original.ToString());
                    }
                }
            }

            ComponentUtility.CopyComponent(clone);
            ComponentUtility.PasteComponentAsNew(destination);
            DestroyImmediate(host);
        }

        private Component FindComponent(Transform root, System.Type type, string name)
        {
            if (root.name == name)
            {
                Component found = root.gameObject.GetComponent(type);
                if (found)
                    return found;
            }
            foreach (Transform transform in root)
            {
                Component found = FindComponent(transform, type, name);
                if (found)
                    return found;
            }
            return null;
        }

        private class ComponentEnumerator : IEnumerable<ComponentEnumerator.Result>
        {
            public struct Result
            {
                public GameObject source;
                public GameObject destination;
                public Component component;
            }
            private GameObject _source;
            private GameObject _destination;
            private string _component;

            public ComponentEnumerator(GameObject source, GameObject destination, string component)
            {
                _source = source;
                _destination = destination;
                _component = component;
            }

            public IEnumerator<Result> GetEnumerator()
            {
                Result result;
                result.source = _source;
                result.destination = _destination;

                foreach (var component in result.source.GetComponents(typeof(Component)))
                {
                    if (component.ToString().EndsWith(_component))
                    {
                        result.component = component;
                        yield return result;
                    }
                }

                foreach (Transform sourceTransform in _source.transform)
                {
                    var sourceName = sourceTransform.gameObject.name;
                    bool found = false;
                    foreach (Transform destinationTransform in _destination.transform)
                    {
                        if (sourceName != destinationTransform.gameObject.name)
                            continue;
                        if (sourceTransform.gameObject.GetType() != destinationTransform.gameObject.GetType())
                            continue;
                        found = true;
                        result.source = sourceTransform.gameObject;
                        result.destination = destinationTransform.gameObject;
                        break;
                    }
                    if (!found)
                        continue;
                    var i = new ComponentEnumerator(sourceTransform.gameObject, result.destination.gameObject, _component).GetEnumerator();
                    while (i.MoveNext())
                        yield return i.Current;
                }
                yield break;
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}