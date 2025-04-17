import json
import os


def get_config(
    local_config_path="./appsettings.local.json",
    default_config_path="./appsettings.json",
):
    """
    Loads configuration from appsettings.local.json if it exists, otherwise from appsettings.json.
    Returns the config as a dict, or None if neither file exists.
    """
    if os.path.exists(local_config_path):
        print(f"Reading settings from {local_config_path}")
        with open(local_config_path, "r", encoding="utf-8") as f:
            app_settings = json.load(f)
    elif os.path.exists(default_config_path):
        print(f"Reading settings from {default_config_path}")
        with open(default_config_path, "r", encoding="utf-8") as f:
            app_settings = json.load(f)
    else:
        print("No configuration files found. Will use hardcoded values.")
        return None
    print("Settings loaded successfully")
    return app_settings
