using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    /// <summary>Local review screenshots only; no uploads, credentials or room codes in filenames.</summary>
    public sealed class VisualCapture : MonoBehaviour
    {
        void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard==null||!keyboard.f2Key.wasPressedThisFrame||Application.isBatchMode)return;
            var game=GetComponent<PrototypeGame>();if(!game||!game.InWorld)return;
            try
            {
                string folder=Path.Combine(Application.persistentDataPath,"Screenshots");Directory.CreateDirectory(folder);
                string path=Path.Combine(folder,"LestingCraft-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".png");
                ScreenCapture.CaptureScreenshot(path);Debug.Log("[Screenshot] Requested: "+path);
            }
            catch(Exception error){Debug.LogWarning("[Screenshot] Cannot save: "+error.Message);}
        }
    }
}
