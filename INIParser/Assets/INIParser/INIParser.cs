using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class INIParser
{
    #region"名称定义"
    // Error: 在出现错误的情况下，这将更改为其他值而不是1
    // Error codes: 
    // 1: Null TextAsset
    public int error = 0;
    //锁用于线程安全访问文件和本地缓存 ***
    private object m_Lock = new object();
    // 文件名
    private string m_FileName = null;
    public string FileName
    {
        get
        {
            return m_FileName;
        }
    }
    //把ini转换成字符串
    private string m_iniString = null;
    public string iniString
    {
        get
        {
            return m_iniString;
        }
    }
    //自动清除
    private bool m_AutoFlush = false;
    //本地缓存
    private Dictionary<string, Dictionary<string, string>> m_Sections = new Dictionary<string, Dictionary<string, string>>();
    private Dictionary<string, Dictionary<string, string>> m_Modified = new Dictionary<string, Dictionary<string, string>>();
    //本地缓存修改标志
    private bool m_CacheModified = false;
    #endregion
    #region "方法和接口"
    //通过路径打开ini
    public void Open(string path)
    {
        m_FileName = path;

        if (File.Exists(m_FileName))
        {
            m_iniString = File.ReadAllText(m_FileName);
        }
        else
        {
            //检查文件是否存在
            var temp = File.Create(m_FileName);
            temp.Close();
            m_iniString = "";
        }

        Initialize(m_iniString, false);
    }
    //本地打开ini文件通过TextAsset:所有的更改保存到本地存储修改标志
    public void Open(TextAsset name)
    {
        if (name == null)
        {
            //如果为空，则将其视为打开的空文件
            error = 1;
            m_iniString = "";
            m_FileName = null;
            Initialize(m_iniString, false);
        }
        else
        {
            m_FileName = Application.persistentDataPath + name.name;

            //首先在本地存储中找到TextAsset
            if (File.Exists(m_FileName))
            {
                m_iniString = File.ReadAllText(m_FileName);
            }
            else m_iniString = name.text;
            Initialize(m_iniString, false);
        }
    }

    //以字符串的形式打开ini
    public void OpenFromString(string str)
    {
        m_FileName = null;
        Initialize(str, false);
    }

    //获取ini文件的字符串内容
    public override string ToString()
    {
        return m_iniString;
    }

    private void Initialize(string iniString, bool AutoFlush)
    {
        m_iniString = iniString;
        m_AutoFlush = AutoFlush;
        Refresh();
    }

    //关闭，保存ini文件内容的所有更改
    public void Close()
    {
        lock (m_Lock)
        {
            PerformFlush();

            //清除记忆缓存
            m_FileName = null;
            m_iniString = null;
        }
    }

    // 解析部分的名字
    private string ParseSectionName(string Line)
    {
        if (!Line.StartsWith("[")) return null;
        if (!Line.EndsWith("]")) return null;
        if (Line.Length < 3) return null;
        return Line.Substring(1, Line.Length - 2);
    }

    //解析稀疏键+值对的名称
    private bool ParseKeyValuePair(string Line, ref string Key, ref string Value)
    {
        //检查值对键+值对名称
        int i;
        if ((i = Line.IndexOf('=')) <= 0) return false;

        int j = Line.Length - i - 1;
        Key = Line.Substring(0, i).Trim();
        if (Key.Length <= 0) return false;

        Value = (j > 0) ? (Line.Substring(i + 1, j).Trim()) : ("");
        return true;
    }

    //如果一行既不是SectionName也不是key+value对，那么它就是一条注释
    private bool isComment(string Line)
    {
        string tmpKey = null, tmpValue = null;
        if (ParseSectionName(Line) != null) return false;
        if (ParseKeyValuePair(Line, ref tmpKey, ref tmpValue)) return false;
        return true;
    }

    //将文件内容读入本地缓存
    private void Refresh()
    {
        lock (m_Lock)
        {
            StringReader sr = null;
            try
            {
                //清除本地缓存
                m_Sections.Clear();
                m_Modified.Clear();
                //文本读取
                sr = new StringReader(m_iniString);
                //阅读文件内容
                Dictionary<string, string> CurrentSection = null;
                string s;
                string SectionName;
                string Key = null;
                string Value = null;
                while ((s = sr.ReadLine()) != null)
                {
                    s = s.Trim();

                    //检查节名
                    SectionName = ParseSectionName(s);
                    if (SectionName != null)
                    {
                        //只加载第一次出现的部分
                        if (m_Sections.ContainsKey(SectionName))
                        {
                            CurrentSection = null;
                        }
                        else
                        {
                            CurrentSection = new Dictionary<string, string>();
                            m_Sections.Add(SectionName, CurrentSection);
                        }
                    }
                    else if (CurrentSection != null)
                    {
                        //检查键值对
                        if (ParseKeyValuePair(s, ref Key, ref Value))
                        {
                            //只加载第一次出现的键
                            if (!CurrentSection.ContainsKey(Key))
                            {
                                CurrentSection.Add(Key, Value);
                            }
                        }
                    }
                }
            }
            finally
            {
                // 清除并关闭文件
                if (sr != null) sr.Close();
                sr = null;
            }
        }
    }

    private void PerformFlush()
    {
        //如果没有修改本地缓存，则退出
        if (!m_CacheModified) return;
        m_CacheModified = false;

        //将原始iniString的内容复制到临时字符串中，替换修改后的值
        StringWriter sw = new StringWriter();

        try
        {
            Dictionary<string, string> CurrentSection = null;
            Dictionary<string, string> CurrentSection2 = null;
            StringReader sr = null;
            try
            {
                //打开原始文件
                sr = new StringReader(m_iniString);

                //读取文件的原始内容，用本地缓存值替换更改
                string s;
                string SectionName;
                string Key = null;
                string Value = null;
                bool Unmodified;
                bool Reading = true;

                bool Deleted = false;
                string Key2 = null;
                string Value2 = null;

                StringBuilder sb_temp;

                while (Reading)
                {
                    s = sr.ReadLine();
                    Reading = (s != null);

                    //检查iniString是否结束
                    if (Reading)
                    {
                        Unmodified = true;
                        s = s.Trim();
                        SectionName = ParseSectionName(s);
                    }
                    else
                    {
                        Unmodified = false;
                        SectionName = null;
                    }

                    //检查节名
                    if ((SectionName != null) || (!Reading))
                    {
                        if (CurrentSection != null)
                        {
                            //在离开一个节之前，写入所有剩余的修改值
                            if (CurrentSection.Count > 0)
                            {
                                //可选:删除新值和区段之前的所有空行
                                sb_temp = sw.GetStringBuilder();
                                while ((sb_temp[sb_temp.Length - 1] == '\n') || (sb_temp[sb_temp.Length - 1] == '\r'))
                                {
                                    sb_temp.Length = sb_temp.Length - 1;
                                }
                                sw.WriteLine();

                                foreach (string fkey in CurrentSection.Keys)
                                {
                                    if (CurrentSection.TryGetValue(fkey, out Value))
                                    {
                                        sw.Write(fkey);
                                        sw.Write('=');
                                        sw.WriteLine(Value);
                                    }
                                }
                                sw.WriteLine();
                                CurrentSection.Clear();
                            }
                        }

                        if (Reading)
                        {
                            //检查当前部分是否在本地修改缓存中
                            if (!m_Modified.TryGetValue(SectionName, out CurrentSection))
                            {
                                CurrentSection = null;
                            }
                        }
                    }
                    else if (CurrentSection != null)
                    {
                        //检查键值对
                        if (ParseKeyValuePair(s, ref Key, ref Value))
                        {
                            if (CurrentSection.TryGetValue(Key, out Value))
                            {
                                //将修改后的值写入临时文件
                                Unmodified = false;
                                CurrentSection.Remove(Key);

                                sw.Write(Key);
                                sw.Write('=');
                                sw.WriteLine(Value);
                            }
                        }
                    }

                    //检查当前行的section/key是否已被删除
                    if (Unmodified)
                    {
                        if (SectionName != null)
                        {
                            if (!m_Sections.ContainsKey(SectionName))
                            {
                                Deleted = true;
                                CurrentSection2 = null;
                            }
                            else
                            {
                                Deleted = false;
                                m_Sections.TryGetValue(SectionName, out CurrentSection2);
                            }

                        }
                        else if (CurrentSection2 != null)
                        {
                            if (ParseKeyValuePair(s, ref Key2, ref Value2))
                            {
                                if (!CurrentSection2.ContainsKey(Key2)) Deleted = true;
                                else Deleted = false;
                            }
                        }
                    }


                    //从原始的iniString中写入未修改的行
                    if (Unmodified)
                    {
                        if (isComment(s)) sw.WriteLine(s);
                        else if (!Deleted) sw.WriteLine(s);
                    }
                }

                //关闭字符串读者
                sr.Close();
                sr = null;
            }
            finally
            {
                //关闭stringreader                
                if (sr != null) sr.Close();
                sr = null;
            }

            //循环所有剩余的修改值
            foreach (KeyValuePair<string, Dictionary<string, string>> SectionPair in m_Modified)
            {
                CurrentSection = SectionPair.Value;
                if (CurrentSection.Count > 0)
                {
                    sw.WriteLine();

                    //编写节名
                    sw.Write('[');
                    sw.Write(SectionPair.Key);
                    sw.WriteLine(']');

                    //循环该节中的所有键+值对
                    foreach (KeyValuePair<string, string> ValuePair in CurrentSection)
                    {
                        //写入键+值对
                        sw.Write(ValuePair.Key);
                        sw.Write('=');
                        sw.WriteLine(ValuePair.Value);
                    }
                    CurrentSection.Clear();
                }
            }
            m_Modified.Clear();

            //获取结果到iniString
            m_iniString = sw.ToString();
            sw.Close();
            sw = null;

            //将iniString写入文件
            if (m_FileName != null)
            {
                File.WriteAllText(m_FileName, m_iniString);
            }
        }
        finally
        {
            //清理:关闭字符串写入器               
            if (sw != null) sw.Close();
            sw = null;
        }
    }

    //检查该节是否存在
    public bool IsSectionExists(string SectionName)
    {
        return m_Sections.ContainsKey(SectionName);
    }

    //检查密钥是否存在
    public bool IsKeyExists(string SectionName, string Key)
    {
        Dictionary<string, string> Section;

        //检查该节是否存在
        if (m_Sections.ContainsKey(SectionName))
        {
            m_Sections.TryGetValue(SectionName, out Section);

            // 如果秘钥存在
            return Section.ContainsKey(Key);
        }
        else return false;
    }

    //删除本地缓存中的一个区段
    public void SectionDelete(string SectionName)
    {
        //检查是否存在
        if (IsSectionExists(SectionName))
        {
            lock (m_Lock)
            {
                m_CacheModified = true;
                m_Sections.Remove(SectionName);

                //如果存在，也删除已修改的缓存
                m_Modified.Remove(SectionName);

                //自动刷新:立即写入文件的任何修改
                if (m_AutoFlush) PerformFlush();
            }
        }
    }

    //删除本地缓存中的密钥
    public void KeyDelete(string SectionName, string Key)
    {
        Dictionary<string, string> Section;

        //删除关键字(如果存在)
        if (IsKeyExists(SectionName, Key))
        {
            lock (m_Lock)
            {
                m_CacheModified = true;
                m_Sections.TryGetValue(SectionName, out Section);
                Section.Remove(Key);

                //如果存在，也删除已修改的缓存
                if (m_Modified.TryGetValue(SectionName, out Section)) Section.Remove(SectionName);

                //自动刷新:立即写入文件的任何修改
                if (m_AutoFlush) PerformFlush();
            }
        }

    }

    //从本地缓存中读取一个值
    public string ReadValue(string SectionName, string Key, string DefaultValue)
    {
        lock (m_Lock)
        {
            //检查该节是否存在
            Dictionary<string, string> Section;
            if (!m_Sections.TryGetValue(SectionName, out Section)) return DefaultValue;

            //检查密钥是否存在
            string Value;
            if (!Section.TryGetValue(Key, out Value)) return DefaultValue;

            //返回找到的值
            return Value;
        }
    }

    //在本地缓存中插入或修改一个值
    public void WriteValue(string SectionName, string Key, string Value)
    {
        lock (m_Lock)
        {
            //标记本地缓存修改
            m_CacheModified = true;

            //检查是否存在字节
            Dictionary<string, string> Section;
            if (!m_Sections.TryGetValue(SectionName, out Section))
            {
                //如何不在，添加
                Section = new Dictionary<string, string>();
                m_Sections.Add(SectionName, Section);
            }

            //修改值
            if (Section.ContainsKey(Key)) Section.Remove(Key);
            Section.Add(Key, Value);

            //将修改值添加到本地修改值缓存中
            if (!m_Modified.TryGetValue(SectionName, out Section))
            {
                Section = new Dictionary<string, string>();
                m_Modified.Add(SectionName, Section);
            }

            if (Section.ContainsKey(Key)) Section.Remove(Key);
            Section.Add(Key, Value);

            // 自动刷新:立即写入文件的任何修改
            if (m_AutoFlush) PerformFlush();
        }
    }

    //编码字节数组
    private string EncodeByteArray(byte[] Value)
    {
        if (Value == null) return null;

        StringBuilder sb = new StringBuilder();
        foreach (byte b in Value)
        {
            string hex = Convert.ToString(b, 16);
            int l = hex.Length;
            if (l > 2)
            {
                sb.Append(hex.Substring(l - 2, 2));
            }
            else
            {
                if (l < 2) sb.Append("0");
                sb.Append(hex);
            }
        }
        return sb.ToString();
    }

    //解码字节数组
    private byte[] DecodeByteArray(string Value)
    {
        if (Value == null) return null;

        int l = Value.Length;
        if (l < 2) return new byte[] { };

        l /= 2;
        byte[] Result = new byte[l];
        for (int i = 0; i < l; i++) Result[i] = Convert.ToByte(Value.Substring(i * 2, 2), 16);
        return Result;
    }

    //得到各种类型的数据 
    public bool ReadValue(string SectionName, string Key, bool DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        int Value;
        if (int.TryParse(StringValue, out Value)) return (Value != 0);
        return DefaultValue;
    }

    public int ReadValue(string SectionName, string Key, int DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        int Value;
        if (int.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
        return DefaultValue;
    }

    public long ReadValue(string SectionName, string Key, long DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        long Value;
        if (long.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
        return DefaultValue;
    }

    public double ReadValue(string SectionName, string Key, double DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        double Value;
        if (double.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out Value)) return Value;
        return DefaultValue;
    }

    public byte[] ReadValue(string SectionName, string Key, byte[] DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, EncodeByteArray(DefaultValue));
        try
        {
            return DecodeByteArray(StringValue);
        }
        catch (FormatException)
        {
            return DefaultValue;
        }
    }

    public DateTime ReadValue(string SectionName, string Key, DateTime DefaultValue)
    {
        string StringValue = ReadValue(SectionName, Key, DefaultValue.ToString(CultureInfo.InvariantCulture));
        DateTime Value;
        if (DateTime.TryParse(StringValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AssumeLocal, out Value)) return Value;
        return DefaultValue;
    }

    //写入各种类型
    public void WriteValue(string SectionName, string Key, bool Value)
    {
        WriteValue(SectionName, Key, (Value) ? ("1") : ("0"));
    }

    public void WriteValue(string SectionName, string Key, int Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteValue(string SectionName, string Key, long Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteValue(string SectionName, string Key, double Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteValue(string SectionName, string Key, byte[] Value)
    {
        WriteValue(SectionName, Key, EncodeByteArray(Value));
    }

    public void WriteValue(string SectionName, string Key, DateTime Value)
    {
        WriteValue(SectionName, Key, Value.ToString(CultureInfo.InvariantCulture));
    }

    #endregion
}


