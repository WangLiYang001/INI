using UnityEngine;
using System;

public class IniRead : MonoBehaviour
{
    public string INIPath;
    public static IniRead Instance;
    public float Aa;
    public float Bb;
    void Awake()
    {
        Instance = this;
        INIPath = Application.streamingAssetsPath + "/键位配置.ini";
        IniReadFile(INIPath);
    }
    void IniReadFile(string path)
    {
        INIParser iniParser = new INIParser();
        iniParser.Open(path);
        Aa = Convert.ToSingle(iniParser.ReadValue("AA", "a", 0d));
        Bb = Convert.ToSingle(iniParser.ReadValue("Bb", "b", 0d));
        Debug.Log("Aa="+Aa);
        Debug.Log("Bb=" + Bb);
        iniParser.Close();
    }
}