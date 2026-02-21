"""
PHASE 3 — Proof of Concept: Finding H-1
===========================================
Proves that CsvWriter.WriteLoop() has a logic bug on line 37 where it
checks `!Directory.Exists(_filePath)` instead of `!Directory.Exists(directoryName)`.

Since this is C# code, we replicate the exact logic in Python to demonstrate
the semantic bug without requiring a Unity runtime.
"""
import os
import tempfile
import pytest


def csv_writer_directory_check_BUGGY(file_path: str) -> str:
    """
    Exact replica of CsvWriter.WriteLoop() lines 36-40 (BUGGY version).
    Returns what action was taken.
    """
    directory_name = os.path.dirname(file_path)
    
    # Bug: checks if the FILE PATH exists as a directory, not the DIRECTORY
    if directory_name and not os.path.isdir(file_path):  # ← BUG: should be directory_name
        os.makedirs(directory_name, exist_ok=True)
        return "created_directory"
    else:
        return "skipped"


def csv_writer_directory_check_CORRECT(file_path: str) -> str:
    """
    What the code SHOULD do — check if the directory exists.
    """
    directory_name = os.path.dirname(file_path)
    
    if directory_name and not os.path.isdir(directory_name):  # ← CORRECT
        os.makedirs(directory_name, exist_ok=True)
        return "created_directory"
    else:
        return "skipped"


class TestH1_CsvWriterDirectoryCheck:
    """Proves H-1: CsvWriter checks file path instead of directory path."""

    def test_01_buggy_always_creates_when_file_doesnt_exist_as_dir(self, tmp_path):
        """
        PROOF: The buggy code ALWAYS runs CreateDirectory because a
        file path (e.g., "/data/session/poses.csv") is never a directory.
        """
        # Directory already exists
        existing_dir = tmp_path / "session"
        existing_dir.mkdir()
        
        file_path = str(existing_dir / "poses.csv")
        
        # Buggy version: checks if "poses.csv" exists as directory → always False
        result = csv_writer_directory_check_BUGGY(file_path)
        assert result == "created_directory", (
            "PROOF: Buggy code runs CreateDirectory even when parent dir already exists "
            "because it checks the FILE path, not the DIRECTORY path"
        )

        # Correct version: checks if "session/" directory exists → True, skips
        result = csv_writer_directory_check_CORRECT(file_path)
        assert result == "skipped", (
            "Correct code skips when directory already exists"
        )

    def test_02_buggy_skips_when_file_path_is_somehow_a_directory(self, tmp_path):
        """
        PROOF: If the file path somehow exists as a directory (naming conflict),
        the buggy code SKIPS directory creation, and the subsequent file write
        would fail or behave unexpectedly.
        """
        # Create a directory named "poses.csv" (adversarial edge case)
        session_dir = tmp_path / "session"
        session_dir.mkdir()
        
        fake_file_as_dir = session_dir / "poses.csv"
        fake_file_as_dir.mkdir()  # This is now a directory, not a file!
        
        file_path = str(fake_file_as_dir)
        
        # Buggy version: checks if "poses.csv" directory exists → True, SKIPS
        result = csv_writer_directory_check_BUGGY(file_path)
        assert result == "skipped", (
            "PROOF: When file path IS a directory, buggy code skips creation "
            "and StreamWriter would fail trying to open a directory as a file"
        )

    def test_03_semantic_difference_matters(self, tmp_path):
        """
        PROOF: The semantic difference between checking the file path vs 
        directory path matters in the edge case where the directory 
        does NOT yet exist.
        """
        # Neither the directory nor the file exists
        file_path = str(tmp_path / "new_session" / "data" / "poses.csv")
        
        # Buggy version: file_path is not a dir → creates parent directory ✓
        # (This works by accident because the condition is always True)
        result_buggy = csv_writer_directory_check_BUGGY(file_path)
        assert result_buggy == "created_directory"
        assert os.path.isdir(os.path.dirname(file_path))
        
        # Clean up for correct test
        os.rmdir(os.path.dirname(file_path))
        os.rmdir(os.path.dirname(os.path.dirname(file_path)))
        
        # Correct version: directory doesn't exist → creates it ✓
        result_correct = csv_writer_directory_check_CORRECT(file_path)
        assert result_correct == "created_directory"
        
        # Both create the directory, but the BUGGY version does it for the 
        # WRONG REASON (file doesn't exist as dir) while the CORRECT version
        # does it for the RIGHT REASON (directory doesn't exist)


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
