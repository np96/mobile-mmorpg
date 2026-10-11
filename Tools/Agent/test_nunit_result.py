"""NUnit acceptance-policy regression fixtures are generated in memory."""

import contextlib
import io
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

from nunit_result import main, validate_test_run


def make_run(passed=1, ignored=0, failed=0, inconclusive=0, result=None):
    root = ET.Element("test-run", {
        "result": result or ("Skipped:Ignored" if ignored else "Passed"),
        "total": str(passed + ignored + failed + inconclusive),
        "passed": str(passed), "skipped": str(ignored),
        "failed": str(failed), "inconclusive": str(inconclusive),
    })
    for count, status in ((passed, "Passed"), (ignored, "Skipped"),
                          (failed, "Failed"), (inconclusive, "Inconclusive")):
        for index in range(count):
            case = ET.SubElement(root, "test-case", {
                "result": status, "name": status + str(index),
                "classname": "Fixture", "methodname": status,
            })
            if status == "Skipped":
                case.set("label", "Ignored")
                ET.SubElement(ET.SubElement(case, "reason"), "message").text = "Source not selected."
    return root


class NUnitResultTests(unittest.TestCase):
    def test_accepts_all_executed_passed(self):
        counts, ignored = validate_test_run(make_run(passed=3))
        self.assertEqual(counts["passed"], 3)
        self.assertEqual(ignored, [])

    def test_accepts_expected_ignored_root_with_nonzero_executed_tests(self):
        counts, ignored = validate_test_run(make_run(passed=1047, ignored=5))
        self.assertEqual(counts["total"], 1052)
        self.assertEqual(len(ignored), 5)

    def test_accepts_passed_root_with_explicit_ignored_leaf_tests(self):
        self.assertEqual(validate_test_run(make_run(ignored=1, result="Passed"))[0]["skipped"], 1)

    def test_rejects_empty_run(self):
        with self.assertRaises(ValueError):
            validate_test_run(make_run(passed=0))

    def test_rejects_all_ignored_run(self):
        with self.assertRaises(ValueError):
            validate_test_run(make_run(passed=0, ignored=5))

    def test_rejects_failed_tests_even_if_root_claims_pass(self):
        with self.assertRaises(ValueError):
            validate_test_run(make_run(failed=1))

    def test_rejects_inconclusive_tests_even_if_root_claims_pass(self):
        with self.assertRaises(ValueError):
            validate_test_run(make_run(inconclusive=1))

    def test_rejects_unsupported_root_results(self):
        for result in ("Failed", "Inconclusive", "Skipped", "Error", ""):
            with self.subTest(result=result), self.assertRaises(ValueError):
                root = make_run()
                root.set("result", result)
                validate_test_run(root)

    def test_rejects_wrong_root_element(self):
        with self.assertRaises(ValueError):
            validate_test_run(ET.Element("test-suite"))

    def test_rejects_missing_or_invalid_counts(self):
        for value in (None, "-1", "", "one", "1.5"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                root = make_run()
                if value is None:
                    del root.attrib["passed"]
                else:
                    root.set("passed", value)
                validate_test_run(root)

    def test_rejects_unaccounted_total(self):
        root = make_run()
        root.set("total", "2")
        with self.assertRaises(ValueError):
            validate_test_run(root)

    def test_rejects_ignored_root_without_ignored_tests(self):
        with self.assertRaises(ValueError):
            validate_test_run(make_run(result="Skipped:Ignored"))

    def test_rejects_skipped_leaf_without_ignored_label(self):
        root = make_run(ignored=1)
        del root.findall("test-case")[1].attrib["label"]
        with self.assertRaises(ValueError):
            validate_test_run(root)

    def test_rejects_missing_leaf_test_cases(self):
        root = make_run()
        root.remove(root.find("test-case"))
        with self.assertRaises(ValueError):
            validate_test_run(root)

    def test_rejects_leaf_results_inconsistent_with_summary(self):
        root = make_run(passed=2)
        root.findall("test-case")[1].set("result", "Inconclusive")
        with self.assertRaises(ValueError):
            validate_test_run(root)

    def test_reports_ignored_separately_without_claiming_it_passed(self):
        output = io.StringIO()
        with mock.patch("nunit_result.ET.parse", return_value=ET.ElementTree(make_run(ignored=1))), contextlib.redirect_stdout(output):
            self.assertEqual(main(["unused.xml"]), 0)
        self.assertIn("passed=1", output.getvalue())
        self.assertIn("ignored=1", output.getvalue())
        self.assertIn("ignored (not run): Fixture.Skipped: Source not selected.", output.getvalue())

    def test_rejects_malformed_xml(self):
        with mock.patch("nunit_result.ET.parse", side_effect=ET.ParseError("malformed")), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(main(["unused.xml"]), 1)

    def test_rejects_missing_xml(self):
        with mock.patch("nunit_result.ET.parse", side_effect=OSError("missing")), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(main(["unused.xml"]), 1)


if __name__ == "__main__":
    unittest.main()
