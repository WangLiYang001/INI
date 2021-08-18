using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class INIParser
{
    #region"���ƶ���"
    // Error: �ڳ��ִ��������£��⽫����Ϊ����ֵ������1
    // Error codes: 
    // 1: Null TextAsset
    public int error = 0;
    //�������̰߳�ȫ�����ļ��ͱ��ػ��� ***
    private object m_Lock = new object();
    // �ļ���
    private string m_FileName = null;
    public string FileName
    {
        get
        {
            return m_FileName;
        }
    }
    //��iniת�����ַ���
    private string m_iniString = null;
    public string iniString
    {
        get
        {
            return m_iniString;
        }
    }
    //�Զ����
    private bool m_AutoFlush = false;
    //���ػ���
    private Dictionary<string, Dictionary<string, string>> m_Sections = new Dictionary<string, Dictionary<string, string>>();
    private Dictionary<string, Dictionary<string, string>> m_Modified = new Dictionary<string, Dictionary<string, string>>();
    //���ػ����޸ı�־
    private bool m_CacheModified = false;
    #endregion
    #region "�����ͽӿ�"
    //ͨ��·����ini
    public void Open(string path)
    {
        m_FileName = path;

        if (File.Exists(m_FileName))
        {
            m_iniString = File.ReadAllText(m_FileName);
        }
        else
        {
            //����ļ��Ƿ����
            var temp = File.Create(m_FileName);
            temp.Close();
            m_iniString = "";
        }

        Initialize(m_iniString, false);
    }
    //���ش�ini�ļ�ͨ��TextAsset:���еĸ��ı��浽���ش洢�޸ı�־
    public void Open(TextAsset name)
    {
        if (name == null)
        {
            //���Ϊ�գ�������Ϊ�򿪵Ŀ��ļ�
            error = 1;
            m_iniString = "";
            m_FileName = null;
            Initialize(m_iniString, false);
        }
        else
        {
            m_FileName = Application.persistentDataPath + name.name;

            //�����ڱ��ش洢���ҵ�TextAsset
            if (File.Exists(m_FileName))
            {
                m_iniString = File.ReadAllText(m_FileName);
            }
            else m_iniString = name.text;
            Initialize(m_iniString, false);
        }
    }

    //���ַ�������ʽ��ini
    public void OpenFromString(string str)
    {
        m_FileName = null;
        Initialize(str, false);
    }

    //��ȡini�ļ����ַ�������
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

    //�رգ�����ini�ļ����ݵ����и���
    public void Close()
    {
        lock (m_Lock)
        {
            PerformFlush();

            //������仺��
            m_FileName = null;
            m_iniString = null;
        }
    }

    // �������ֵ�����
    private string ParseSectionName(string Line)
    {
        if (!Line.StartsWith("[")) return null;
        if (!Line.EndsWith("]")) return null;
        if (Line.Length < 3) return null;
        return Line.Substring(1, Line.Length - 2);
    }

    //����ϡ���+ֵ�Ե�����
    private bool ParseKeyValuePair(string Line, ref string Key, ref string Value)
    {
        //���ֵ�Լ�+ֵ������
        int i;
        if ((i = Line.IndexOf('=')) <= 0) return false;

        int j = Line.Length - i - 1;
        Key = Line.Substring(0, i).Trim();
        if (Key.Length <= 0) return false;

        Value = (j > 0) ? (Line.Substring(i + 1, j).Trim()) : ("");
        return true;
    }

    //���һ�мȲ���SectionNameҲ����key+value�ԣ���ô������һ��ע��
    private bool isComment(string Line)
    {
        string tmpKey = null, tmpValue = null;
        if (ParseSectionName(Line) != null) return false;
        if (ParseKeyValuePair(Line, ref tmpKey, ref tmpValue)) return false;
        return true;
    }

    //���ļ����ݶ��뱾�ػ���
    private void Refresh()
    {
        lock (m_Lock)
        {
            StringReader sr = null;
            try
            {
                //������ػ���
                m_Sections.Clear();
                m_Modified.Clear();
                //�ı���ȡ
                sr = new StringReader(m_iniString);
                //�Ķ��ļ�����
                Dictionary<string, string> CurrentSection = null;
                string s;
                string SectionName;
                string Key = null;
                string Value = null;
                while ((s = sr.ReadLine()) != null)
                {
                    s = s.Trim();

                    //������
                    SectionName = ParseSectionName(s);
                    if (SectionName != null)
                    {
                        //ֻ���ص�һ�γ��ֵĲ���
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
                        //����ֵ��
                        if (ParseKeyValuePair(s, ref Key, ref Value))
                        {
                            //ֻ���ص�һ�γ��ֵļ�
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
                // ������ر��ļ�
                if (sr != null) sr.Close();
                sr = null;
            }
        }
    }

    private void PerformFlush()
    {
        //���û���޸ı��ػ��棬���˳�
        if (!m_CacheModified) return;
        m_CacheModified = false;

        //��ԭʼiniString�����ݸ��Ƶ���ʱ�ַ����У��滻�޸ĺ��ֵ
        StringWriter sw = new StringWriter();

        try
        {
            Dictionary<string, string> CurrentSection = null;
            Dictionary<string, string> CurrentSection2 = null;
            StringReader sr = null;
            try
            {
                //��ԭʼ�ļ�
                sr = new StringReader(m_iniString);

                //��ȡ�ļ���ԭʼ���ݣ��ñ��ػ���ֵ�滻����
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

                    //���iniString�Ƿ����
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

                    //������
                    if ((SectionName != null) || (!Reading))
                    {
                        if (CurrentSection != null)
                        {
                            //���뿪һ����֮ǰ��д������ʣ����޸�ֵ
                            if (CurrentSection.Count > 0)
                            {
                                //��ѡ:ɾ����ֵ������֮ǰ�����п���
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
                            //��鵱ǰ�����Ƿ��ڱ����޸Ļ�����
                            if (!m_Modified.TryGetValue(SectionName, out CurrentSection))
                            {
                                CurrentSection = null;
                            }
                        }
                    }
                    else if (CurrentSection != null)
                    {
                        //����ֵ��
                        if (ParseKeyValuePair(s, ref Key, ref Value))
                        {
                            if (CurrentSection.TryGetValue(Key, out Value))
                            {
                                //���޸ĺ��ֵд����ʱ�ļ�
                                Unmodified = false;
                                CurrentSection.Remove(Key);

                                sw.Write(Key);
                                sw.Write('=');
                                sw.WriteLine(Value);
                            }
                        }
                    }

                    //��鵱ǰ�е�section/key�Ƿ��ѱ�ɾ��
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


                    //��ԭʼ��iniString��д��δ�޸ĵ���
                    if (Unmodified)
                    {
                        if (isComment(s)) sw.WriteLine(s);
                        else if (!Deleted) sw.WriteLine(s);
                    }
                }

                //�ر��ַ�������
                sr.Close();
                sr = null;
            }
            finally
            {
                //�ر�stringreader                
                if (sr != null) sr.Close();
                sr = null;
            }

            //ѭ������ʣ����޸�ֵ
            foreach (KeyValuePair<string, Dictionary<string, string>> SectionPair in m_Modified)
            {
                CurrentSection = SectionPair.Value;
                if (CurrentSection.Count > 0)
                {
                    sw.WriteLine();

                    //��д����
                    sw.Write('[');
                    sw.Write(SectionPair.Key);
                    sw.WriteLine(']');

                    //ѭ���ý��е����м�+ֵ��
                    foreach (KeyValuePair<string, string> ValuePair in CurrentSection)
                    {
                        //д���+ֵ��
                        sw.Write(ValuePair.Key);
                        sw.Write('=');
                        sw.WriteLine(ValuePair.Value);
                    }
                    CurrentSection.Clear();
                }
            }
            m_Modified.Clear();

            //��ȡ�����iniString
            m_iniString = sw.ToString();
            sw.Close();
            sw = null;

            //��iniStringд���ļ�
            if (m_FileName != null)
            {
                File.WriteAllText(m_FileName, m_iniString);
            }
        }
        finally
        {
            //����:�ر��ַ���д����               
            if (sw != null) sw.Close();
            sw = null;
        }
    }

    //���ý��Ƿ����
    public bool IsSectionExists(string SectionName)
    {
        return m_Sections.ContainsKey(SectionName);
    }

    //�����Կ�Ƿ����
    public bool IsKeyExists(string SectionName, string Key)
    {
        Dictionary<string, string> Section;

        //���ý��Ƿ����
        if (m_Sections.ContainsKey(SectionName))
        {
            m_Sections.TryGetValue(SectionName, out Section);

            // �����Կ����
            return Section.ContainsKey(Key);
        }
        else return false;
    }

    //ɾ�����ػ����е�һ������
    public void SectionDelete(string SectionName)
    {
        //����Ƿ����
        if (IsSectionExists(SectionName))
        {
            lock (m_Lock)
            {
                m_CacheModified = true;
                m_Sections.Remove(SectionName);

                //������ڣ�Ҳɾ�����޸ĵĻ���
                m_Modified.Remove(SectionName);

                //�Զ�ˢ��:����д���ļ����κ��޸�
                if (m_AutoFlush) PerformFlush();
            }
        }
    }

    //ɾ�����ػ����е���Կ
    public void KeyDelete(string SectionName, string Key)
    {
        Dictionary<string, string> Section;

        //ɾ���ؼ���(�������)
        if (IsKeyExists(SectionName, Key))
        {
            lock (m_Lock)
            {
                m_CacheModified = true;
                m_Sections.TryGetValue(SectionName, out Section);
                Section.Remove(Key);

                //������ڣ�Ҳɾ�����޸ĵĻ���
                if (m_Modified.TryGetValue(SectionName, out Section)) Section.Remove(SectionName);

                //�Զ�ˢ��:����д���ļ����κ��޸�
                if (m_AutoFlush) PerformFlush();
            }
        }

    }

    //�ӱ��ػ����ж�ȡһ��ֵ
    public string ReadValue(string SectionName, string Key, string DefaultValue)
    {
        lock (m_Lock)
        {
            //���ý��Ƿ����
            Dictionary<string, string> Section;
            if (!m_Sections.TryGetValue(SectionName, out Section)) return DefaultValue;

            //�����Կ�Ƿ����
            string Value;
            if (!Section.TryGetValue(Key, out Value)) return DefaultValue;

            //�����ҵ���ֵ
            return Value;
        }
    }

    //�ڱ��ػ����в�����޸�һ��ֵ
    public void WriteValue(string SectionName, string Key, string Value)
    {
        lock (m_Lock)
        {
            //��Ǳ��ػ����޸�
            m_CacheModified = true;

            //����Ƿ�����ֽ�
            Dictionary<string, string> Section;
            if (!m_Sections.TryGetValue(SectionName, out Section))
            {
                //��β��ڣ����
                Section = new Dictionary<string, string>();
                m_Sections.Add(SectionName, Section);
            }

            //�޸�ֵ
            if (Section.ContainsKey(Key)) Section.Remove(Key);
            Section.Add(Key, Value);

            //���޸�ֵ��ӵ������޸�ֵ������
            if (!m_Modified.TryGetValue(SectionName, out Section))
            {
                Section = new Dictionary<string, string>();
                m_Modified.Add(SectionName, Section);
            }

            if (Section.ContainsKey(Key)) Section.Remove(Key);
            Section.Add(Key, Value);

            // �Զ�ˢ��:����д���ļ����κ��޸�
            if (m_AutoFlush) PerformFlush();
        }
    }

    //�����ֽ�����
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

    //�����ֽ�����
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

    //�õ��������͵����� 
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

    //д���������
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


