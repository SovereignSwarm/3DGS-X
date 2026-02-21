"""Root conftest.py for PerseusXR test suite.

Automatically adds the reconstruction scripts directory to PYTHONPATH
so that tests can import utils, models, etc. without manual path setup.
"""

import sys
from pathlib import Path

# Add perseusxr-reconstruction/scripts to PYTHONPATH for import resolution
SCRIPTS_DIR = Path(__file__).resolve().parent.parent / "perseusxr-reconstruction" / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))
