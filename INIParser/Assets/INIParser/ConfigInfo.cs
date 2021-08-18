using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Text;

namespace IniParser
{
#if UNITY_EDITOR
    using UnityEditor;
    public class ConfigInfo : EditorWindow
    {
        //存取路径
        string filePath;
        //文件名称
        string filename = "";
        // ini文件包含： section=配置节，key=键名，value=键值
        string Section = "";
        string Key = "";
        string Value = "";
        //信息查询
        bool ShowInfo;
      
       
        private static void Init()
        {
            ConfigInfo window = GetWindowWithRect<ConfigInfo>(new Rect(0, 0, 450, 220));
            window.titleContent = new GUIContent("INIParser");
            window.Show();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space();   // 空一行
            EditorGUILayout.Space();
            filename = EditorGUILayout.TextField(" 文件名称 : ", filename);

            EditorGUILayout.Space();
            Section = EditorGUILayout.TextField(" 配置节 : ", Section);

            EditorGUILayout.Space();
            Key = EditorGUILayout.TextField(" 键名 : ", Key);

            EditorGUILayout.Space();
            Value = EditorGUILayout.TextField(" 键值 ：", Value);
            Repaint(); //实时刷新

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            //if (GUILayout.Button("保存位置"))
            //{
            //    filePath = EditorUtility.OpenFolderPanel("", "", "");
               
            //}
            filePath = Application.streamingAssetsPath;
            EditorGUILayout.Space();
            if (GUILayout.Button("添加INI配置文件"))
            {
                TakeShot();
            }
            EditorGUILayout.Space();

            if (GUILayout.Button("打开导出文件夹"))
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    Debug.LogError("<color=red>" + "没有选择INI文件保存位置" + "</color>");
                    return;
                }
                Application.OpenURL("file://" + filePath);
            }
            EditorGUILayout.Space();
            //ShowInfo = EditorGUILayout.Toggle("内容查询", ShowInfo);
            //if (ShowInfo)
            //{
            //   // Debug.Log("查询成功");
             

            //} 
        }
        private void TakeShot()
        {

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("<color=red>" + "没有选择INI保存位置" + "</color>");
                return;
            }
            else
            {
                if ( string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(Section) || string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(Value))
                {
                    Debug.LogError("<color=red>" + "内容不能为空" + "</color>");
                    return;
                }
                else
                {
                    INIParser ini = new INIParser();

                    ini.Open(filePath + "/" + filename + "." + "ini");
                    ini.WriteValue(Section, Key, Value);
                    ini.Close();
                    Debug.Log("添加成功");
                }
               
            }
          
        }



    }
#endif
}
