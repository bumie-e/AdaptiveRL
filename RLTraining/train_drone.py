import os
import subprocess
from sys import platform
from mlagents_envs.registry import UnityEnvRegistry
from mlagents_envs.registry.remote_registry_entry import get_local_binary_path

def main():
    # 1. Load the local registry
    registry = UnityEnvRegistry()
    
    # Path handling to ensure it works from root or subfolder
    registry_file = "registry.yaml"
    if not os.path.exists(registry_file):
        registry_file = "../registry.yaml"
    
    if not os.path.exists(registry_file):
        print("Error: registry.yaml not found.")
        return

    registry.register_from_yaml(registry_file)

    # 2. Get the environment entry
    if "drone-v1" not in registry:
        print("Error: 'drone-v1' not found in registry.yaml")
        return
        
    env_desc = registry["drone-v1"]
    
    print("Resolving environment executable path...")
    
    # Determine the URL based on platform
    url = None
    if platform == "linux" or platform == "linux2":
        url = env_desc._linux_url
    elif platform == "darwin":
        url = env_desc._darwin_url
    elif platform == "win32":
        url = env_desc._win_url

    if not url:
        print(f"Error: No URL found for platform {platform} in registry.")
        return

    # Use the same utility ML-Agents uses internally to download/extract
    executable_path = get_local_binary_path(env_desc.identifier, url)
    print(f"Executable resolved to: {executable_path}")
    
    # 3. Path to the training config
    config_path = "drone_training_config.yaml"
    if not os.path.exists(config_path):
        config_path = "../drone_training_config.yaml"

    # 4. Build env-args from registry (passed to the Unity binary, not mlagents-learn)
    env_args = getattr(env_desc, "_additional_args", []) or []

    # 5. Launch mlagents-learn
    # Increased timeout-wait to 300s to allow 8 envs to startup
    cmd = [
        "mlagents-learn", config_path,
        "--env=" + str(executable_path),
        "--run-id=Drone_Remote_v1",
        "--force",
        "--timeout-wait=300"
    ]

    if env_args:
        cmd += ["--env-args"] + env_args

    # On headless Linux, wrap with xvfb-run so Camera.Render() has a virtual display.
    # -nographics would break camera capture, so we use Xvfb instead.
    if platform in ("linux", "linux2"):
        cmd = ["xvfb-run", "-a"] + cmd

    print(f"\n[INFO] Starting training using YAML config.")
    print(f"[CMD] {' '.join(cmd)}\n")

    try:
        subprocess.run(cmd, check=True)
    except KeyboardInterrupt:
        print("\nTraining interrupted by user.")
    except Exception as e:
        print(f"\nAn error occurred during training: {e}")

if __name__ == "__main__":
    main()
