from importlib import metadata

__all__ = ["__version__"]

try:
    __version__ = metadata.version("toska-mesh-cli")
except metadata.PackageNotFoundError:  # During development/editable installs
    __version__ = "0.0.0"
