using UnityEditor;
using UnityEngine;

namespace Pancake.Editor
{
    [CustomEditor(typeof(ServiceSettings))]
    public class PlayfabEditor : UnityEditor.Editor
    {
        public static bool callFromEditorWindow = false;
        private Uniform.Property _titleId;
        private Uniform.Property _secretKey;
        private Uniform.Property _requestType;
        private Uniform.Property _enableAdminApi;
        private Uniform.Property _enableClientApi;
        private Uniform.Property _enableEntityApi;
        private Uniform.Property _enableServerApi;
        private Uniform.Property _enableRequestTimesApi;
        private Uniform.Property _infoRequestParams;

        private void Init()
        {
            _titleId = new Uniform.Property(serializedObject.FindProperty("titleId"), new GUIContent("Title Id", "Title id of project"));
            _secretKey = new Uniform.Property(serializedObject.FindProperty("secretKey"), new GUIContent("Secret Key", "Title secret key"));
            _requestType = new Uniform.Property(serializedObject.FindProperty("requestType"), new GUIContent("Request Type", "Request type"));
            _enableAdminApi = new Uniform.Property(serializedObject.FindProperty("enableAdminApi"), new GUIContent("Admin API", "Enable admin api"));
            _enableClientApi = new Uniform.Property(serializedObject.FindProperty("enableClientApi"), new GUIContent("Client API", "Enable client api"));
            _enableEntityApi = new Uniform.Property(serializedObject.FindProperty("enableEntityApi"), new GUIContent("Entity API", "Enable entity api"));
            _enableServerApi = new Uniform.Property(serializedObject.FindProperty("enableServerApi"), new GUIContent("Server API", "Enable server api"));
            _enableRequestTimesApi = new Uniform.Property(serializedObject.FindProperty("enableRequestTimesApi"),
                new GUIContent("Request Times API", "Enable request time api"));
            _infoRequestParams = new Uniform.Property(serializedObject.FindProperty("infoRequestParams"), new GUIContent("Info Request Param"));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Init();
            if (!callFromEditorWindow)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "This ScriptableObject holds all the settings of Playfab. Please go to menu Tools > Pancake > Playfab or click the button below to edit it.",
                    MessageType.Info);
                if (GUILayout.Button("Edit")) PlayfabWindow.ShowWindow();
                return;
            }

            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);

            Uniform.DrawUppercaseSection("PLAYFAB_SETTING",
                "SETTING",
                () =>
                {
                    EditorGUILayout.PropertyField(_titleId.property, _titleId.content);
                    EditorGUILayout.PropertyField(_secretKey.property, _secretKey.content);
                    EditorGUILayout.PropertyField(_requestType.property, _requestType.content);

                    ServiceSettings.SharedSettings.TitleId = ServiceSettings.TitleId;
#if ENABLE_PLAYFABSERVER_API || ENABLE_PLAYFABADMIN_API || UNITY_EDITOR
                    ServiceSettings.SharedSettings.DeveloperSecretKey = ServiceSettings.SecretKey;
#endif
                    ServiceSettings.SharedSettings.RequestType = ServiceSettings.RequestType;
                    Uniform.SpaceOneLine();
                    EditorGUILayout.PropertyField(_infoRequestParams.property, _infoRequestParams.content);
                });
            Uniform.SpaceOneLine();
            Uniform.DrawUppercaseSection("PLAYFAB_FEATURE",
                "API & FEATURE",
                () =>
                {
                    EditorGUILayout.PropertyField(_enableAdminApi.property, _enableAdminApi.content);
                    EditorGUILayout.PropertyField(_enableClientApi.property, _enableClientApi.content);
                    EditorGUILayout.PropertyField(_enableEntityApi.property, _enableEntityApi.content);
                    EditorGUILayout.PropertyField(_enableServerApi.property, _enableServerApi.content);
                    Uniform.SpaceTwoLine();
                    EditorGUILayout.PropertyField(_enableRequestTimesApi.property, _enableRequestTimesApi.content);

                    if (ServiceSettings.EnableAdminApi) ScriptingDefinition.AddDefineSymbolOnAllPlatforms(PlayfabConstant.ENABLE_PLAYFABADMIN_API);
                    else ScriptingDefinition.RemoveDefineSymbolOnAllPlatforms(PlayfabConstant.ENABLE_PLAYFABADMIN_API);

                    if (ServiceSettings.EnableClientApi) ScriptingDefinition.RemoveDefineSymbolOnAllPlatforms(PlayfabConstant.DISABLE_PLAYFABCLIENT_API);
                    else ScriptingDefinition.AddDefineSymbolOnAllPlatforms(PlayfabConstant.DISABLE_PLAYFABCLIENT_API);

                    if (ServiceSettings.EnableEntityApi) ScriptingDefinition.RemoveDefineSymbolOnAllPlatforms(PlayfabConstant.DISABLE_PLAYFABENTITY_API);
                    else ScriptingDefinition.AddDefineSymbolOnAllPlatforms(PlayfabConstant.DISABLE_PLAYFABENTITY_API);

                    if (ServiceSettings.EnableServerApi) ScriptingDefinition.AddDefineSymbolOnAllPlatforms(PlayfabConstant.ENABLE_PLAYFABSERVER_API);
                    else ScriptingDefinition.RemoveDefineSymbolOnAllPlatforms(PlayfabConstant.ENABLE_PLAYFABSERVER_API);

                    if (ServiceSettings.EnableRequestTimesApi) ScriptingDefinition.AddDefineSymbolOnAllPlatforms(PlayfabConstant.PLAYFAB_REQUEST_TIMING);
                    else ScriptingDefinition.RemoveDefineSymbolOnAllPlatforms(PlayfabConstant.PLAYFAB_REQUEST_TIMING);
                });

            EditorGUI.EndDisabledGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }
}