import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('scan', Path(__file__).parents[2] / 'scripts/security_scan.py')
scan = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scan)

class Gates(unittest.TestCase):
    def test_no_nuget_dependencies(self):
        self.assertEqual(scan.nuget_gate({'version': 1, 'projects': []}), [])

    def test_nuget_transitive_high(self):
        data = {'version': 1, 'projects': [{'frameworks': [{'transitivePackages': [{'id': 'Example', 'vulnerabilities': [{'severity': 'High'}]}]}]}]}
        self.assertEqual(len(scan.nuget_gate(data)), 1)

    def test_low_not_blocked(self):
        data = {'version': 1, 'projects': [{'frameworks': [{'topLevelPackages': [{'id': 'Example', 'vulnerabilities': [{'severity': 'Low'}]}]}]}]}
        self.assertEqual(scan.nuget_gate(data), [])

    def test_feed_error_not_pass(self):
        with self.assertRaises(ValueError):
            scan.nuget_gate({'version': 1, 'projects': [], 'problems': [{'message': 'Feed unavailable'}]})

    def test_invalid_nuget_report(self):
        with self.assertRaises(ValueError): scan.nuget_gate({})

    def test_sarif_score(self):
        data = {'version': '2.1.0', 'runs': [{'tool': {'driver': {'rules': [{'id': 'r', 'properties': {'security-severity': '8.5'}}]}}, 'results': [{'ruleId': 'r'}]}]}
        self.assertEqual(scan.sarif_gate(data), ['r'])
        data['runs'][0]['tool']['driver']['rules'][0]['properties']['security-severity'] = '5.0'
        self.assertEqual(scan.sarif_gate(data), [])

    def test_sarif_analysis_error(self):
        data = {'version': '2.1.0', 'runs': [{'tool': {'driver': {}}, 'invocations': [{'executionSuccessful': False}]}]}
        with self.assertRaises(ValueError): scan.sarif_gate(data)

    def test_unknown_sarif_rule(self):
        data = {'version': '2.1.0', 'runs': [{'tool': {'driver': {}}, 'results': [{'ruleId': 'missing'}]}]}
        with self.assertRaises(ValueError): scan.sarif_gate(data)

    def test_missing_sarif(self):
        with self.assertRaises(ValueError): scan.sarif_gate({'version': '2.1.0', 'runs': []})

if __name__ == '__main__': unittest.main()
