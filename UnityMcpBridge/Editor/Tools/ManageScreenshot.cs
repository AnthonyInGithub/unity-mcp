using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcpBridge.Editor.Helpers;

namespace UnityMcpBridge.Editor.Tools
{
    /// <summary>
    /// Handles screenshot operations for Unity cameras.
    /// </summary>
    public static class ManageScreenshot
    {
        /// <summary>
        /// Main handler for screenshot management actions.
        /// </summary>
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString().ToLower() ?? "capture";
            string cameraName = @params["cameraName"]?.ToString();
            int? width = @params["width"]?.ToObject<int?>();
            int? height = @params["height"]?.ToObject<int?>();
            string format = @params["format"]?.ToString()?.ToUpper() ?? "PNG";

            try
            {
                switch (action)
                {
                    case "capture":
                        return CaptureScreenshot(cameraName, width, height, format);
                    
                    case "list_cameras":
                        return ListCameras();
                    
                    default:
                        return Response.Error($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ManageScreenshot] Exception in HandleCommand: {ex.Message}");
                UnityEngine.Debug.LogError($"[ManageScreenshot] Stack trace: {ex.StackTrace}");
                return Response.Error($"Error in screenshot management: {ex.Message} | Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Captures a screenshot from the specified camera (or main camera if none specified).
        /// </summary>
        private static object CaptureScreenshot(string cameraName, int? width, int? height, string format)
        {
            try
            {
                // Find the target camera
                Camera targetCamera = FindCamera(cameraName);
                if (targetCamera == null)
                {
                    string errorMsg = string.IsNullOrEmpty(cameraName) 
                        ? "No main camera found in the scene. Please ensure a camera is present and tagged as 'MainCamera'."
                        : $"Camera '{cameraName}' not found in the scene.";
                    return Response.Error(errorMsg);
                }

                // Determine screenshot dimensions
                int screenshotWidth = width ?? (int)targetCamera.pixelWidth;
                int screenshotHeight = height ?? (int)targetCamera.pixelHeight;

                // Validate dimensions
                if (screenshotWidth <= 0 || screenshotHeight <= 0)
                {
                    screenshotWidth = 1920; // Default width
                    screenshotHeight = 1080; // Default height
                }
                // Create render texture
                RenderTexture renderTexture = new RenderTexture(screenshotWidth, screenshotHeight, 24);
                RenderTexture.active = renderTexture;

                // Store original camera settings
                RenderTexture originalTargetTexture = targetCamera.targetTexture;
                
                try
                {
                    // Set camera to render to our texture
                    targetCamera.targetTexture = renderTexture;
                    targetCamera.Render();

                    // Read pixels from render texture
                    Texture2D screenshot = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
                    screenshot.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
                    screenshot.Apply();

                    // Resize to 320x180 thumbnail for faster transmission
                    Texture2D thumbnail = ResizeTexture(screenshot, 320, 180);
                    
                    // Clean up original full-size screenshot
                    UnityEngine.Object.DestroyImmediate(screenshot);

                    // Convert thumbnail to bytes based on format
                    byte[] imageBytes;
                    string actualFormat;
                    
                    if (format == "JPG" || format == "JPEG")
                    {
                        imageBytes = thumbnail.EncodeToJPG(75); // 75% quality
                        actualFormat = "JPG";
                    }
                    else
                    {
                        imageBytes = thumbnail.EncodeToPNG();
                        actualFormat = "PNG";
                    }

                    // Convert to base64
                    string base64Image = System.Convert.ToBase64String(imageBytes);

                    // Clean up thumbnail
                    UnityEngine.Object.DestroyImmediate(thumbnail);

                    // Return in proper MCP content format for LLM visual processing
                    return new
                    {
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Screenshot captured from camera '{targetCamera.name}' at 320x180 resolution (original: {screenshotWidth}x{screenshotHeight}) in {actualFormat} format."
                            },
                            new
                            {
                                type = "image",
                                data = base64Image,
                                mimeType = actualFormat == "JPG" ? "image/jpeg" : "image/png"
                            }
                        }
                    };
                }
                finally
                {
                    // Restore original camera settings
                    targetCamera.targetTexture = originalTargetTexture;
                    RenderTexture.active = null;
                    
                    // Clean up render texture
                    if (renderTexture != null)
                    {
                        renderTexture.Release();
                        UnityEngine.Object.DestroyImmediate(renderTexture);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ManageScreenshot] Exception in CaptureScreenshot: {ex.Message}");
                UnityEngine.Debug.LogError($"[ManageScreenshot] CaptureScreenshot Stack trace: {ex.StackTrace}");
                return Response.Error($"Failed to capture screenshot: {ex.Message} | Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Lists all cameras in the current scene.
        /// </summary>
        private static object ListCameras()
        {
            try
            {
                Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                
                var cameraList = cameras.Select(cam => new
                {
                    name = cam.name,
                    isMainCamera = cam.CompareTag("MainCamera"),
                    isActive = cam.gameObject.activeInHierarchy,
                    resolution = $"{(int)cam.pixelWidth}x{(int)cam.pixelHeight}",
                    renderingPath = cam.renderingPath.ToString(),
                    depth = cam.depth
                }).OrderByDescending(c => c.isMainCamera).ThenBy(c => c.name).ToArray();

                var responseData = new
                {
                    cameras = cameraList,
                    totalCount = cameraList.Length,
                    mainCameraFound = cameraList.Any(c => c.isMainCamera)
                };

                string message = cameraList.Length > 0 
                    ? $"Found {cameraList.Length} camera(s) in the scene."
                    : "No cameras found in the current scene.";

                return Response.Success(message, responseData);
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list cameras: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds a camera by name, or returns the main camera if no name is specified.
        /// </summary>
        private static Camera FindCamera(string cameraName)
        {
            if (string.IsNullOrEmpty(cameraName))
            {
                // Try to find main camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                    return mainCamera;
                
                // Fallback: find any camera tagged as MainCamera
                GameObject mainCameraGO = GameObject.FindWithTag("MainCamera");
                if (mainCameraGO != null)
                {
                    Camera camera = mainCameraGO.GetComponent<Camera>();
                    if (camera != null)
                        return camera;
                }
                
                // Last resort: find any active camera
                Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                return cameras.FirstOrDefault(c => c.gameObject.activeInHierarchy);
            }
            else
            {
                // Find camera by name
                GameObject cameraGO = GameObject.Find(cameraName);
                if (cameraGO != null)
                {
                    Camera camera = cameraGO.GetComponent<Camera>();
                    if (camera != null)
                        return camera;
                }
                
                // Try finding among all cameras
                Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                return cameras.FirstOrDefault(c => c.name.Equals(cameraName, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Resizes a Texture2D to the specified dimensions using bilinear filtering.
        /// </summary>
        private static Texture2D ResizeTexture(Texture2D original, int newWidth, int newHeight)
        {
            // Create a new texture with the target size
            Texture2D resized = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            
            // Calculate scaling factors
            float xScale = (float)original.width / newWidth;
            float yScale = (float)original.height / newHeight;
            
            // Sample pixels from original texture
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    // Calculate source coordinates with bilinear sampling
                    float sourceX = x * xScale;
                    float sourceY = y * yScale;
                    
                    // Get color using bilinear interpolation
                    Color pixelColor = original.GetPixelBilinear(sourceX / original.width, sourceY / original.height);
                    resized.SetPixel(x, y, pixelColor);
                }
            }
            
            // Apply changes
            resized.Apply();
            return resized;
        }
    }
} 