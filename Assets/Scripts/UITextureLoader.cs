using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
//using BrandXR.Tools;

#if UIGRADIENT
using JoshH.UI;
#endif

namespace BrandXR.Textures
{
    public class UITextureLoader : MonoBehaviour
    {
        #region VARIABLES

        private Image image;
        private Renderer rend;
        private Color color1;
        private Color color1NoAlpha;

#if UIGRADIENT
        private UIGradient uiGradient;
        private Color color2;
        private Color color2NoAlpha;
#endif

        public bool finishedLoadingTexture { get; private set; } = false;
        public string url;
        public UnityEvent OnSuccess;

        private bool initialUpdateOccured = false;
        private bool finishedDisablingAlpha = false;
        private bool finishedEnablingAlpha = false;

        #endregion

        #region STARTUP LOGIC

        //----------------------------------//
        public void Start()
        //----------------------------------//
        {
#if !KTX
            if( url.Contains(".basis" ) )
            {
                url = url.Replace( ".basis", ".png" );
            }
#endif

            //ClearPersistentAppData.ClearPersistentData();
            LoadTexture();
            
        } //END Start

#endregion

#region LOAD TEXTURE

        //------------------------------------------------------------//
        public void LoadTexture()
        //------------------------------------------------------------//
        {
            if( GetComponent<Image>() != null )
                image = GetComponent<Image>();

            if( GetComponent<Renderer>() != null )
                rend = GetComponent<Renderer>();

            //Debug.Log("Trying to load " + url + " into " + name );

            SpriteLoader.Instance.LoadFromCacheOrDownload
            (
                url,
                ( Sprite sprite, string path ) =>
                {
                    if( image != null )
                    {
                        image.sprite = sprite;
                        image.preserveAspect = true;

                        //Debug.Log("Successfully loaded " + path + " into " + image.name);

#if !UNITY_EDITOR
                        image.SetMaterialDirty();
#endif
                    }
                    if( rend != null )
                    {
                        rend.sharedMaterial.mainTexture = sprite.texture;
                        //Debug.Log( "Successfully loaded " + path + " into " + rend.name );
                    }

                    finishedLoadingTexture = true;

                    OnSuccess?.Invoke();
                },
                ( string error ) =>
                {
                    Debug.LogError( "ERROR CALLBACK = " + error + ", url = " + url );
                },
                ( float progress ) =>
                {
                    //Debug.Log( progress );
                }
            );


        } //END _LoadTexture

#endregion

#region UPDATE
        //-----------------------------------//
        public void Update()
        //-----------------------------------//
        {
            //First time setup, grab the image component
            if( !initialUpdateOccured )
            {
                if( image == null )
                    image = GetComponent<Image>();

                if( image == null )
                    return;

                color1 = new Color( image.color.r, image.color.g, image.color.b, 1f );
                color1NoAlpha = new Color( color1.r, color1.g, color1.b, 0f );

                //See if the image component's color is influenced by a gradient component (overrides image component color)
#if UIGRADIENT
                if( uiGradient == null && GetComponent<UIGradient>() != null )
                {
                    uiGradient = GetComponent<UIGradient>();
                    color1 = new Color( uiGradient.LinearColor1.r, uiGradient.LinearColor1.g, uiGradient.LinearColor1.b, 1f );
                    color2 = new Color( uiGradient.LinearColor2.r, uiGradient.LinearColor2.g, uiGradient.LinearColor2.b, 1f );
                    color1NoAlpha = new Color( color1.r, color1.g, color1.b, 0f );
                    color2NoAlpha = new Color( color2.r, color2.g, color2.b, 0f );
                }
#endif

                initialUpdateOccured = true;
            }


            if( image != null && initialUpdateOccured )
            {
                if( image.sprite == null && !finishedDisablingAlpha )
                {
                    image.color = color1NoAlpha;

#if UIGRADIENT
                    if( uiGradient )
                    {
                        uiGradient.LinearColor1 = color1NoAlpha;
                        uiGradient.LinearColor2 = color2NoAlpha;
                    }
#endif
                    finishedDisablingAlpha = true;
                }
                else if( image.sprite != null && !finishedEnablingAlpha )
                {
                    image.color = color1;

#if UIGRADIENT
                    if( uiGradient )
                    {
                        uiGradient.LinearColor1 = color1;
                        uiGradient.LinearColor2 = color2;
                    }
#endif
                    finishedEnablingAlpha = true;
                }
            }

        } //END Update
#endregion

    } //END UITextureLoader class

} //END namespace