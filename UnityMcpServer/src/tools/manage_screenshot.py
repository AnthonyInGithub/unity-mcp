from mcp.server.fastmcp import FastMCP, Context
from typing import Dict, Any, List
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
    ):
        """Takes screenshots of Unity cameras and returns them as images.

        Args:
            action: Operation (e.g., 'capture', 'list_cameras').
            camera_name: Name of the camera to capture from (defaults to main camera).
            width: Screenshot width in pixels (defaults to camera resolution).
            height: Screenshot height in pixels (defaults to camera resolution).
            format: Image format ('PNG' or 'JPG').

        Returns:
            Dictionary with operation results ('success', 'message', 'data').
            For 'capture' action, includes the screenshot as visual content the LLM can process.
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

            # Unity now returns proper MCP content format directly
            if "content" in response:
                # Return content array directly - let FastMCP handle the format
                return response["content"]
            else:
                # Fallback for other actions (like list_cameras) or error responses
                return {
                    "success": response.get("success", False),
                    "message": response.get("message", "Screenshot operation completed."),
                    "data": response.get("data", {})
                }

        except Exception as e:
            return {"success": False, "message": f"Python error managing screenshot: {str(e)}"} 