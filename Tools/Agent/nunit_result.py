#!/usr/bin/env python3
"""Accept completed NUnit runs, reporting explicitly ignored tests separately."""

import sys
import xml.etree.ElementTree as ET


def validate_test_run(root):
    if root.tag != "test-run":
        raise ValueError("expected a test-run root")
    if root.get("result") not in ("Passed", "Skipped:Ignored"):
        raise ValueError("test-run result is neither Passed nor Skipped:Ignored")

    counts = {}
    for name in ("total", "passed", "failed", "inconclusive", "skipped"):
        value = root.get(name, "")
        if not value.isdecimal():
            raise ValueError("missing or invalid nonnegative count: " + name)
        counts[name] = int(value)

    if counts["total"] == 0 or counts["passed"] == 0:
        raise ValueError("run must contain at least one executed, passing test")
    if counts["failed"] != 0 or counts["inconclusive"] != 0:
        raise ValueError("failed or inconclusive tests remain")
    if counts["total"] != counts["passed"] + counts["skipped"]:
        raise ValueError("total is not fully accounted for by passed and ignored tests")
    if root.get("result") == "Skipped:Ignored" and counts["skipped"] == 0:
        raise ValueError("Skipped:Ignored result has no ignored tests")

    cases = list(root.iter("test-case"))
    if len(cases) != counts["total"]:
        raise ValueError("test-case count does not match total")
    passed = 0
    ignored = []
    for case in cases:
        if case.get("result") == "Passed":
            passed += 1
        elif case.get("result") == "Skipped" and case.get("label") == "Ignored":
            ignored.append(case)
        else:
            raise ValueError("a test-case is failed, inconclusive, or skipped without an Ignored label")
    if passed != counts["passed"] or len(ignored) != counts["skipped"]:
        raise ValueError("leaf results do not match test-run counts")

    return counts, ignored


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    if len(args) != 1:
        print("usage: nunit_result.py <results.xml>", file=sys.stderr)
        return 2
    try:
        counts, ignored = validate_test_run(ET.parse(args[0]).getroot())
    except (OSError, ET.ParseError, ValueError) as error:
        print("invalid NUnit result: " + str(error), file=sys.stderr)
        return 1

    print("total={total} passed={passed} failed={failed} inconclusive={inconclusive} ignored={skipped}".format(**counts))
    for case in ignored:
        identifier = ".".join(filter(None, (case.get("classname"), case.get("methodname"))))
        identifier = identifier or case.get("name", "unnamed test")
        reason = " ".join(case.findtext("reason/message", default="").split())
        print("ignored (not run): " + identifier + (": " + reason if reason else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
