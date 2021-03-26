/*******************************************************
 * TextureLoader.cs
 * 
 * Loads textures from the cache or web.
 * 
 * If you have the KtxUnity plugin installed and the KTX scripting define symbol in project settings, 
 * you can also use this class to load .ktx or .basis textures
 * 
 *******************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Unity.Collections;

#if KTX
using KtxUnity;
#endif

namespace BrandXR.Textures
{
    public class TextureLoader : Singleton<TextureLoader>
    {
        #region VARIABLES
        private const string CACHE_NAME = "Textures";
        private string cacheFolderPath = "";

        public class TextureOrientation
        {
            public bool IsXFlipped = false;
            public bool IsYFlipped = false;
        }
        #endregion

        #region STARTUP LOGIC
        //---------------------------------------------------------------//
        private void Awake()
        //---------------------------------------------------------------//
        {
            SetupCacheFolderPath();

        } //END Awake

        //---------------------------------------------------------------//
        private void SetupCacheFolderPath()
        //---------------------------------------------------------------//
        {
            if (string.IsNullOrEmpty(cacheFolderPath))
            {
                cacheFolderPath = Application.persistentDataPath + Path.DirectorySeparatorChar + CACHE_NAME;
            }

            Directory.CreateDirectory(cacheFolderPath);

        } //END SetupCacheFolderPath
        #endregion

        #region LOAD FROM CACHE OR DOWNLOAD

        //---------------------------------------------------------------//
        /// <summary>
        /// Loads a Texture2D from the devices local storage cache or a web resource.
        /// If the file does not already exist we will download and cache it
        /// </summary>
        /// <param name="url">The URL to the texture</param>
        /// <param name="successCallback">Sends you a Texture2D and the path to the cached texture when the load has completed</param>
        /// <param name="errorCallback">Contains the UnityWebRequest error, or lets you know if your URL is empty</param>
        /// <param name="progressCallback">Returns a float from 0-1 while downloading occurs</param>
        /// <returns></returns>
        public void LoadFromCacheOrDownload(
            string url,
            Action<Texture2D, string, TextureOrientation> successCallback,
            Action<string> errorCallback = null,
            Action<float> progressCallback = null,
            bool tryLoadFromCache = true)
        //-----------------------------------------------------------------------------------------------------------------------------------------------------//
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            LoadFromCacheOrDownloadAsync(url, successCallback, errorCallback, progressCallback, tryLoadFromCache);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        } //END LoadFromCacheOrDownload

        //-----------------------------------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// The underlying IEnumerator that loads a texture from the cache or downloads it. Use the regular LoadFromCacheOrDownload() unless you need the coroutine this method returns to cancel the logic while in progress
        /// </summary>
        /// <param name="url">The URL to the texture</param>
        /// <param name="successCallback">Sends you a Texture2D and the path to the cached texture when the load has completed</param>
        /// <param name="errorCallback">Contains the UnityWebRequest error, or lets you know if your URL is empty</param>
        /// <param name="progressCallback">Returns a float from 0-1 while downloading occurs</param>
        /// <returns></returns>
        public async Task LoadFromCacheOrDownloadAsync(
            string url,
            Action<Texture2D, string, TextureOrientation> successCallback,
            Action<string> errorCallback = null,
            Action<float> progressCallback = null,
            bool tryLoadFromCache = true,
            CancellationToken token = new CancellationToken())
        //-----------------------------------------------------------------------------------------------------------------------------------------------------//
        {
            SetupCacheFolderPath();

            //If the url is empty, we can't continue
            if (string.IsNullOrEmpty(url))
            {
                errorCallback?.Invoke("TextureLoader.cs FromURL() passed in URL is null or empty");
                return;
            }

            //Flag that will prevent us from downloading from the web if we find the texture in our cache
            //We can't use a 'yield break' command inside of our anonymous success function, so this flag does the trick instead.
            bool foundInCache = false;

#if !UNITY_EDITOR && UNITY_WEBGL
            tryLoadFromCache = false;
#endif

            //Should we try to pull from the cache?
            if (tryLoadFromCache)
            {
                string cachePath = Path.GetFullPath(cacheFolderPath + Path.DirectorySeparatorChar + Path.GetFileName(url));
                string requestCachePath = cachePath;

                //On mobile devices we need to add 'File://'
#if !UNITY_EDITOR && ( UNITY_ANDROID || UNITY_IOS || UNITY_PC || UNITY_MAC || UNITY_WEBGL )
            if( !requestCachePath.Contains( "File://" ) ) { requestCachePath = "File://" + requestCachePath; }
#endif

                await RequestTexture
                (
                    requestCachePath,
                    (Texture2D texture, string path, TextureOrientation orientation) =>
                    {
                        //Debug.Log( "TextureLoader.cs LoadFromCacheOrDownload() Restored from cache = " + cachePath );
                        successCallback?.Invoke(texture, path, orientation);
                        foundInCache = true;
                    },
                    (string error) =>
                    {
                        //Debug.Log( "TextureLoader.cs LoadFromCacheOrDownload() Couldn't find in cache = " + cachePath );
                    },
                    null,
                    token
                );

            }

            //We either don't have the file in the cache or we couldn't locate it, let's try to download it from the given URL
            if (!foundInCache)
            {
                await RequestTexture
                (
                    url,
                    (Texture2D texture, string path, TextureOrientation orientation) =>
                    {
                        successCallback?.Invoke(texture, path, orientation);
                    },
                    (string error) =>
                    {
                        errorCallback?.Invoke(error);
                    },
                    (float progress) =>
                    {
                        progressCallback?.Invoke(1f);
                    },
                    token
                );
            }

        } //END LoadFromCacheOrDownload

        #endregion

        #region REQUEST TEXTURE
        //---------------------------------------------------//
        public Task RequestTexture(
            string url,
            Action<Texture2D, string, TextureOrientation> successCallback,
            Action<string> errorCallback = null,
            Action<float> progressCallback = null,
            CancellationToken token = new CancellationToken())
        //---------------------------------------------------//
        {
            return RequestTextureAsync(url, successCallback, errorCallback, progressCallback, token);

        } //END RequestTexture

        //---------------------------------------------------//
        public async Task RequestTextureAsync(
            string url,
            Action<Texture2D, string, TextureOrientation> successCallback,
            Action<string> errorCallback = null,
            Action<float> progressCallback = null,
            CancellationToken token = new CancellationToken())
        //---------------------------------------------------//
        {
            string cachePath = cacheFolderPath + Path.DirectorySeparatorChar + Path.GetFileName(url);
            TextureOrientation bxrOrientation = new TextureOrientation();
            string name = Path.GetFileName(url);
            string mimeType = "image/" + Path.GetExtension(url).Remove(0, 1);

            //.KTX or .BASIS
            if (mimeType == "image/ktx" || mimeType == "image/ktx2" || mimeType == "image/basis")
            {
#if !KTX
                errorCallback?.Invoke( "KTX and basis texture support is not enabled, try enabling 'KTX' scripting define symbol in project settings and make sure KtxUnity plugin is in your project" );
                return;
#else
                //Debug.Log( "About to transcode = " + Path.GetFileName( url ) );

                // Linear color sampling. Needed for non-color value textures (e.g. normal maps)
                bool linearColor = true;

                //Create a KtxUnity plugin TextureBase component, which will handle the transcoding of the bytes to the texture
                TextureBase textureBase = null;

                if (mimeType == "image/ktx" || mimeType == "image/ktx2")
                    textureBase = new KtxTexture();
                else if (mimeType == "image/basis")
                    textureBase = new BasisUniversalTexture();

                TextureResult result;
                
                // Check if the url is a file path or an http(s) url
                Uri urlUri = new Uri(url);

                if( Uri.IsWellFormedUriString( url, UriKind.Absolute ) &&
                   ( urlUri.Scheme == Uri.UriSchemeHttps || urlUri.Scheme == Uri.UriSchemeHttp ) )
                {
                    result = await textureBase.LoadFromUrl( url, linearColor );
                }
                else
                {
                    // If the file exists in cache, try loading it, else invoke error callback
                    if( !File.Exists( cachePath ) )
                    {
                        errorCallback?.Invoke( "Error loading from cache: " + cachePath + " does not exist." );
                        return;
                    }

                    var nativeArray = new NativeArray<byte>( File.ReadAllBytes( cachePath ), KtxNativeInstance.defaultAllocator );
                    result = await textureBase.LoadFromBytes( nativeArray, true );
                    nativeArray.Dispose();
                }

                if (result != null)
                {
                    Texture2D texture = result.texture;
                    texture.name = name;
                    
                    bxrOrientation.IsXFlipped = result.orientation.IsXFlipped();
                    bxrOrientation.IsYFlipped = result.orientation.IsYFlipped();

                    successCallback?.Invoke(texture, url, bxrOrientation);
                }
                else
                {
                    errorCallback?.Invoke("Unable to transcode " + mimeType + " from path = " + Path.GetFileName(url));
                }
#endif
            }
            
            // For .JPG and .PNG, only this logic is executed.
            // For KTX/Basis, this logic is executed in addition to that above because the .basis file needs to be downloaded separately.
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url, false))
            {
                www.SendWebRequest();

                while (!www.isDone)
                {
                    progressCallback?.Invoke(www.downloadProgress);
                    await Task.Yield();
                }

                //Unity never sends a final progress callback, so we do it ourselves
                progressCallback?.Invoke(1f);

                if (www.isNetworkError || www.isHttpError)
                {
                    errorCallback?.Invoke(www.error);
                }
                else
                {
                    //Save our bytes to persistent storage cache
                    //Debug.Log( "TextureLoader.cs IRequestTexture() Saving to cache = " + cachePath );
                    File.WriteAllBytes(cachePath, www.downloadHandler.data);

                    // Only invoke the success callback for .JPG or .PNG
                    if (!(mimeType == "image/ktx" || mimeType == "image/ktx2" || mimeType == "image/basis"))
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(www);
                        tex.name = name;
                        successCallback?.Invoke(tex, url, bxrOrientation);
                    }
                }
            }
            
        } //END RequestTexture
        #endregion

        #region EDITOR OVERRIDES

#if UNITY_EDITOR
        protected override void OnApplicationQuit() { }

        protected override void OnDestroy() { }
#endif

        #endregion

    } //END class

} //END namespace
