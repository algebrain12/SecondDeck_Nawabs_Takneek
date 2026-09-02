using System;
using TMPro;
using UnityEngine;

public class BoatUI2 : MonoBehaviour
{
    public TMP_Text txt;

    public void ChangeName(String NickName)
    {
        txt.text = NickName;
    }
}
