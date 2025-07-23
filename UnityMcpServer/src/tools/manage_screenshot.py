from mcp.server.fastmcp import FastMCP, Context, Image
from typing import Dict, Any
import base64
from unity_connection import get_unity_connection

def register_manage_screenshot_tools(mcp: FastMCP):
    """Register all screenshot management tools with the MCP server."""

    @mcp.tool()
    def manage_screenshot(
        ctx: Context,
        action: str = "capture",
        camera_name: str = None,
        width: int = None,
        height: int = None,
        format: str = "PNG"
    ) -> Dict[str, Any]:
        """Takes screenshots of Unity cameras and returns them as images.

        Args:
            action: Operation (e.g., 'capture', 'list_cameras').
            camera_name: Name of the camera to capture from (defaults to main camera).
            width: Screenshot width in pixels (defaults to camera resolution).
            height: Screenshot height in pixels (defaults to camera resolution).
            format: Image format ('PNG' or 'JPG').

        Returns:
            Dictionary with operation results ('success', 'message', 'data').
            For 'capture' action, includes the screenshot as an Image object.
        """
        try:
            # Prepare parameters, removing None values
            params = {
                "action": action,
                "cameraName": camera_name,
                "width": width,
                "height": height,
                "format": format,
            }
            params = {k: v for k, v in params.items() if v is not None}
            
            # Send command to Unity
            response = get_unity_connection().send_command("manage_screenshot", params)

            # Process response
            if response.get("success"):
                data = response.get("data", {})
                
                # If this is a capture action and we have image data, return it as an Image
                if action == "capture" and "imageData" in data:
                    try:
                        # Decode base64 image data
                        image_data = base64.b64decode(data["imageData"])
                        image_format = data.get("format", "PNG").lower()
                        
                        # Create Image object for MCP
                        screenshot_image = Image(data=image_data)
                        
                        return {
                            "success": True, 
                            "message": response.get("message", "Screenshot captured successfully."),
                            "image": screenshot_image,
                            "metadata": {
                                "camera": data.get("cameraName"),
                                "resolution": f"{data.get('width', 'unknown')}x{data.get('height', 'unknown')}",
                                "format": data.get("format")
                            }
                        }
                    except Exception as img_error:
                        return {
                            "success": False, 
                            "message": f"Error processing screenshot image: {str(img_error)}"
                        }
                else:
                    # For non-capture actions or when no image data
                    return {
                        "success": True, 
                        "message": response.get("message", "Screenshot operation successful."), 
                        "data": data
                    }
            else:
                return {"success": False, "message": response.get("error", "An unknown error occurred during screenshot operation.")}

        except Exception as e:
            return {"success": False, "message": f"Python error managing screenshot: {str(e)}"} 