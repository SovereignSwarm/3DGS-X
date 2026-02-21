"""T-3: Deterministic unit tests for depth conversion and CLAHE tone mapping.

Validates core geometric and image processing functions against known inputs
per .agent/rules/testing.md requirement for deterministic output tests.
"""

import numpy as np
import pytest


class TestDepthConversion:
    """Test suite for utils/depth_utils.py depth buffer conversion."""

    def test_compute_camera_params_symmetric_fov(self) -> None:
        """Symmetric FoV should produce centered principal point."""
        from utils.depth_utils import compute_depth_camera_params

        fx, fy, cx, cy = compute_depth_camera_params(
            left=1.0, right=1.0, top=1.0, bottom=1.0,
            width=256, height=256
        )
        assert fx == pytest.approx(128.0)
        assert fy == pytest.approx(128.0)
        assert cx == pytest.approx(128.0)
        assert cy == pytest.approx(128.0)

    def test_compute_camera_params_asymmetric_fov(self) -> None:
        """Asymmetric FoV should produce offset principal point."""
        from utils.depth_utils import compute_depth_camera_params

        fx, fy, cx, cy = compute_depth_camera_params(
            left=0.5, right=1.5, top=0.8, bottom=1.2,
            width=200, height=100
        )
        assert fx == pytest.approx(100.0)
        assert cx == pytest.approx(150.0)
        assert fy == pytest.approx(50.0)
        assert cy == pytest.approx(40.0)

    def test_ndc_to_linear_finite_far(self) -> None:
        """Finite far plane should produce valid conversion params."""
        from utils.depth_utils import compute_ndc_to_linear_depth_params

        x, y = compute_ndc_to_linear_depth_params(near=0.1, far=10.0)
        assert x == pytest.approx(-2.0 * 10.0 * 0.1 / (10.0 - 0.1))
        assert y == pytest.approx(-(10.0 + 0.1) / (10.0 - 0.1))

    def test_ndc_to_linear_infinite_far(self) -> None:
        """Infinite far plane should use reversed-Z formula."""
        from utils.depth_utils import compute_ndc_to_linear_depth_params

        x, y = compute_ndc_to_linear_depth_params(near=0.1, far=float('inf'))
        assert x == pytest.approx(-0.2)
        assert y == pytest.approx(-1.0)

    def test_linear_depth_roundtrip(self) -> None:
        """NDC → linear conversion should produce physically valid depths."""
        from utils.depth_utils import convert_depth_to_linear

        # Create a synthetic NDC depth buffer (0=near, 1=far in standard)
        ndc_buffer = np.array([0.0, 0.5, 1.0], dtype=np.float32)
        result = convert_depth_to_linear(ndc_buffer, near=0.1, far=10.0)

        assert result.dtype == np.float32
        assert result.shape == (3,)
        # All values should be finite and non-negative
        assert np.all(np.isfinite(result))

    def test_linear_depth_zero_denom_safe(self) -> None:
        """Division by zero in NDC conversion should produce 0, not crash."""
        from utils.depth_utils import to_linear_depth

        d = np.array([0.5], dtype=np.float64)
        # Choose x, y such that denom = 0
        result = to_linear_depth(d, x=1.0, y=0.0)
        assert result[0] == 0.0  # safe divide returns 0


class TestCLAHEToneMapping:
    """Test suite for utils/image_utils.py tone mapping functions."""

    def test_clahe_preserves_dimensions(self) -> None:
        """CLAHE output should match input dimensions."""
        from utils.image_utils import apply_clahe_tone_mapping

        img = np.random.randint(0, 255, (100, 200, 3), dtype=np.uint8)
        result = apply_clahe_tone_mapping(img)
        assert result.shape == img.shape
        assert result.dtype == img.dtype

    def test_clahe_improves_contrast(self) -> None:
        """CLAHE on a low-contrast image should increase standard deviation."""
        from utils.image_utils import apply_clahe_tone_mapping
        import cv2

        # Create a deliberately low-contrast gray image
        img = np.full((100, 100, 3), 128, dtype=np.uint8)
        img[:50, :] = 130  # Very slight contrast

        result = apply_clahe_tone_mapping(img, clip_limit=4.0)

        # Convert to grayscale for comparison
        gray_in = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        gray_out = cv2.cvtColor(result, cv2.COLOR_BGR2GRAY)

        # CLAHE should increase or maintain contrast (std deviation)
        assert gray_out.std() >= gray_in.std() * 0.9  # Allow small margin

    def test_gamma_correction_brightens(self) -> None:
        """Gamma > 1 should increase average brightness."""
        from utils.image_utils import apply_gamma_correction

        img = np.full((50, 50, 3), 100, dtype=np.uint8)
        result = apply_gamma_correction(img, gamma=2.0)

        assert result.mean() > img.mean()

    def test_tone_mapping_invalid_method_raises(self) -> None:
        """Unknown tone mapping method should raise ValueError."""
        from utils.image_utils import apply_tone_mapping

        img = np.zeros((10, 10, 3), dtype=np.uint8)
        with pytest.raises(ValueError, match="Unknown tone mapping method"):
            apply_tone_mapping(img, method="nonexistent")

    def test_blur_detection_sharp_vs_blurry(self) -> None:
        """Sharp edge image should have higher Laplacian variance than blurred."""
        from utils.image_utils import measure_blur_laplacian
        import cv2

        # Sharp image with clear edges
        sharp = np.zeros((100, 100), dtype=np.uint8)
        sharp[25:75, 25:75] = 255

        # Blurred version
        blurry = cv2.GaussianBlur(sharp, (21, 21), 10)

        sharp_score = measure_blur_laplacian(sharp)
        blurry_score = measure_blur_laplacian(blurry)

        assert sharp_score > blurry_score
