from mcp.server.fastmcp import FastMCP, Context
from typing import Dict, Any
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
                    # Return the base64 image data in a format the agent can display
                    image_format = data.get("format", "PNG").lower()
                    base64_data = data["imageData"]
                    
                    # Create a data URI that can be displayed by the agent
                    data_uri = f"data:image/{image_format};base64,{base64_data}"
                    
                    return {
                        "success": True, 
                        "message": response.get("message", "Screenshot captured successfully."),
                        "data": {
                            "image_url": data_uri,
                            "base64_data": base64_data,
                            "camera": data.get("cameraName"),
                            "resolution": f"{data.get('width', 'unknown')}x{data.get('height', 'unknown')}",
                            "format": data.get("format"),
                            "size_kb": len(base64_data) * 3 // 4 // 1024  # Approximate size in KB
                        }
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