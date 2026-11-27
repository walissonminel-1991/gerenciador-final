import json
import pathlib
import sys
import tempfile
import unittest

import cv2
import numpy as np

ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ROOT))

from compare_gf_screenshots import compare_images, main, write_report  # noqa: E402


class ScreenshotComparisonTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self.directory.name)
        self.reference = self.root / "reference.png"
        self.candidate = self.root / "candidate.png"
        image = np.zeros((4, 5, 4), dtype=np.uint8)
        image[:, :, 0] = 12
        image[:, :, 1] = 34
        image[:, :, 2] = 56
        image[:, :, 3] = 255
        cv2.imwrite(str(self.reference), cv2.cvtColor(image, cv2.COLOR_RGBA2BGRA))
        cv2.imwrite(str(self.candidate), cv2.cvtColor(image, cv2.COLOR_RGBA2BGRA))

    def tearDown(self):
        self.directory.cleanup()

    def test_equal_images_pass_in_exact_mode(self):
        result = compare_images(self.reference, self.candidate)
        self.assertTrue(result["passed"])
        self.assertEqual(result["changed_pixels"], 0)
        self.assertEqual(result["max_channel_delta"], 0)

    def test_one_pixel_difference_fails_in_exact_mode(self):
        image = cv2.imread(str(self.candidate), cv2.IMREAD_UNCHANGED)
        image[1, 2, 0] = 57
        cv2.imwrite(str(self.candidate), image)
        result = compare_images(self.reference, self.candidate)
        self.assertFalse(result["passed"])
        self.assertEqual(result["changed_pixels"], 1)
        self.assertEqual(result["max_channel_delta"], 1)

    def test_threshold_and_region_are_explicit(self):
        image = cv2.imread(str(self.candidate), cv2.IMREAD_UNCHANGED)
        image[0, 0, 0] = 57
        image[3, 4, 0] = 20
        cv2.imwrite(str(self.candidate), image)
        result = compare_images(self.reference, self.candidate, threshold=2, region=(0, 0, 2, 2))
        self.assertTrue(result["passed"])
        self.assertEqual(result["changed_pixels"], 0)
        self.assertEqual(result["region"], {"x": 0, "y": 0, "width": 2, "height": 2})

    def test_dimension_mismatch_is_rejected(self):
        image = np.zeros((3, 5, 4), dtype=np.uint8)
        cv2.imwrite(str(self.candidate), image)
        with self.assertRaises(ValueError):
            compare_images(self.reference, self.candidate)

    def test_report_is_json_and_contains_hashes(self):
        result = compare_images(self.reference, self.candidate)
        report = self.root / "report.json"
        write_report(report, result)
        payload = json.loads(report.read_text(encoding="utf-8"))
        self.assertTrue(payload["passed"])
        self.assertEqual(len(payload["reference_sha256"]), 64)
        self.assertEqual(len(payload["candidate_sha256"]), 64)

    def test_unsupported_16_bit_screenshot_is_rejected(self):
        image = np.zeros((4, 5, 4), dtype=np.uint16)
        image[:, :, 3] = 65535
        sixteen_bit = self.root / "sixteen-bit.png"
        cv2.imwrite(str(sixteen_bit), cv2.cvtColor(image, cv2.COLOR_RGBA2BGRA))
        with self.assertRaises(ValueError):
            compare_images(sixteen_bit, self.reference)

    def test_report_cannot_overwrite_an_input_image(self):
        exit_code = main([str(self.reference), str(self.candidate), "--output", str(self.reference)])
        self.assertEqual(exit_code, 2)

    def test_missing_dependency_has_documented_exit_code(self):
        source = (ROOT / "compare_gf_screenshots.py").read_text(encoding="utf-8")
        self.assertIn("return 3", source)


if __name__ == "__main__":
    unittest.main()
