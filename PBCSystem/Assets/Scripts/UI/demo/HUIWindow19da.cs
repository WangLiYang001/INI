﻿using SimpleWebBrowser;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class HUIWindow19da : HUIBase
{
    public GameObject _Browser;
    private string _url;
    public void Exit()
    {
        Shutdown();
        HUITipManager.Instance.PlayExit();
        HUIManager.Instance.Back(_windowPath);
    }

    public void OnEnable()
    {
        string _path = Application.streamingAssetsPath + "/" + "党建网址/19大网址/网址.txt";
        WWW www = new WWW(_path);
        _url = www.text;
        WebBrowser2D webBrowser = _Browser.GetComponent<WebBrowser2D>();
        if (webBrowser == null)
            webBrowser = _Browser.AddComponent<WebBrowser2D>();
        webBrowser.InitEngineNew(1920, 1080, "MainSharedMem", 9000, _url, false);
    }

    public void Shutdown()
    {
        WebBrowser2D webBrowser = _Browser.GetComponent<WebBrowser2D>();
        webBrowser.Shutdown();
        // Destroy(webBrowser);
    }
    public void Open()
    {
        Process.Start(@"C:\Windows\System32\osk.exe");
    }
}
