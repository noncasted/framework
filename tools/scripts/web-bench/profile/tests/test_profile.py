# Tests of profile.py on a small synthetic profile: python3 -m unittest discover -s tools/scripts/web-bench/profile/tests
#
# fixtures/small.cpuprofile, times in ms (100 ms total):
#   (root) > (idle) 10, (program) 5, main 5 > InitPlayerLoopCallbacks()::UpdateScriptRunBehaviourUpdateRegistrator::Forward() > Game_OnUpdate_m<hash> 10
#     > Physics_Step 30, > RuntimeInvoker_x > Render_Sprites 15; main > glDraw 15 > Physics_Step 10
import importlib.util
import json
import os
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

# Loaded by path: the stdlib has a module named profile too.
_spec = importlib.util.spec_from_file_location('profile_cli', os.path.join(os.path.dirname(HERE), 'profile.py'))
cli = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cli)
from cpuprofile import Profile, ProfileError  # noqa: E402

SMALL = os.path.join(HERE, 'fixtures', 'small.cpuprofile')
LOOP = 'InitPlayerLoopCallbacks()::UpdateScriptRunBehaviourUpdateRegistrator::Forward()'
GAME = 'Game_OnUpdate_m0123456789ABCDEF0123456789ABCDEF01234567'


def run(*argv, warnings=None):
    lines = []
    cli.main(list(argv), out=lines.append, warn=(warnings if warnings is not None else []).append)
    return lines


class ProfileTests(unittest.TestCase):
    def test_self_and_inclusive(self):
        self_t, incl = Profile(SMALL).self_and_inclusive()
        self.assertEqual(self_t['Physics_Step'], 40000)
        self.assertEqual(self_t[GAME], 10000)
        self.assertEqual(self_t['main'], 5000)
        self.assertEqual(incl['Physics_Step'], 40000)  # once per stack
        self.assertEqual(incl[GAME], 55000)
        self.assertEqual(incl['main'], 85000)
        self.assertEqual(incl['(root)'], 100000)

    def test_top(self):
        out = run('top', SMALL, 'Physics|glDraw', '2')
        self.assertEqual(out, ['total ms 100', '--self top', '    40.0 Physics_Step', '    15.0 Render_Sprites',
                               '--incl matching', '    40.0 Physics_Step', '    25.0 glDraw'])

    def test_top_limits_inclusive(self):
        out = run('top', SMALL, '.', '1')
        self.assertEqual(out, ['total ms 100', '--self top', '    40.0 Physics_Step', '--incl matching', '   100.0 (root)'])

    def test_named_profile_no_warning(self):
        warnings = []
        run('top', SMALL, warnings=warnings)
        self.assertEqual(warnings, [])

    def test_unnamed_profile_warns(self):
        data = self._data()
        for i, n in enumerate(data['nodes'][3:]):
            n['callFrame']['functionName'] = f'wasm-function[{i}]'
        warnings = []
        run('top', self._write(data), warnings=warnings)
        self.assertEqual(len(warnings), 1)
        self.assertIn('debug-wasm.sh', warnings[0])

    def test_frames_from_stand_summary(self):
        folder = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, folder)
        shutil.copy(SMALL, os.path.join(folder, 'cpu_20.cpuprofile'))
        with open(os.path.join(folder, 'summary.json'), 'w') as f:
            json.dump({'steps': [{'kind': 'shot', 'file': 'shot_20.png'},
                                 {'kind': 'prof', 'file': 'cpu_20.cpuprofile', 'frames': 10}]}, f)
        out = run('phases', os.path.join(folder, 'cpu_20.cpuprofile'))
        self.assertEqual(out[0], 'total 100 ms, busy 85 ms, frames 10, busy/frame 8.50')

    def test_summary_without_prof_frames_fails(self):
        folder = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, folder)
        shutil.copy(SMALL, os.path.join(folder, 'cpu_20.cpuprofile'))
        with open(os.path.join(folder, 'summary.json'), 'w') as f:
            json.dump({'steps': [{'kind': 'prof', 'file': 'cpu_20.cpuprofile'}]}, f)
        with self.assertRaises(ProfileError):
            run('phases', os.path.join(folder, 'cpu_20.cpuprofile'))

    def test_callers(self):
        out = run('callers', SMALL, 'Physics', '3')
        self.assertEqual(out, [f'   30.0 Physics_Step <- {GAME} <- {LOOP[:70]}',
                               '   10.0 Physics_Step <- glDraw <- main'])

    def test_kids_skip_wrappers(self):
        out = run('kids', SMALL, 'Game_OnUpdate', '--frames', '10')
        self.assertEqual(out[0], '5.50 ms/f under Game_OnUpdate')
        self.assertEqual(out[1:], ['   3.00  54.5% Physics_Step', '   1.50  27.3% Render_Sprites',
                                   '   1.00  18.2% (self)'])

    def test_selfunder(self):
        out = run('selfunder', SMALL, '^main$', '--frame-ms', '10')  # 100 ms / 10 ms = 10 frames
        self.assertEqual(out[0], '8.50 ms/f in ^main$')
        self.assertEqual(out[1], '  4.00  47.1% Physics_Step')

    def test_no_match_fails(self):
        with self.assertRaises(ProfileError):
            run('kids', SMALL, 'Nothing', '--frames', '1')

    def test_phases(self):
        out = run('phases', SMALL, '--frames', '10')
        self.assertEqual(out[0], 'total 100 ms, busy 85 ms, frames 10, busy/frame 8.50')
        self.assertIn('   5.50 ms/f   64.7%  Update', out)
        self.assertIn('   1.00 ms/f   11.8%  other:(idle)', out)

    def test_run_rows(self):
        inc = Profile(SMALL).run_rows()
        self.assertEqual(inc['#busy'], 85000)
        self.assertEqual(inc['#(idle)'], 10000)
        self.assertEqual(inc['#phase Update'], 55000)
        self.assertEqual(inc['#phase outside loop (GL js)'], 30000)
        self.assertEqual(inc['Game_OnUpdate'], 55000)  # the il2cpp hash removed

    def _data(self):
        with open(SMALL) as f:
            return json.load(f)

    def _scaled(self, factor_index, delta):
        data = self._data()
        data['timeDeltas'][factor_index] = delta
        return self._write(data)

    def _write(self, data):
        tmp = tempfile.NamedTemporaryFile('w', suffix='.cpuprofile', delete=False)
        json.dump(data, tmp)
        tmp.close()
        self.addCleanup(os.unlink, tmp.name)
        return tmp.name

    def test_cmp(self):
        slower = self._scaled(3, 40000)  # Physics_Step under Game +20 ms
        rows = tempfile.NamedTemporaryFile('w', suffix='.tsv', delete=False)
        rows.write('#busy\nphysics\tPhysics\n')
        rows.close()
        self.addCleanup(os.unlink, rows.name)
        out = run('cmp', SMALL, slower, '--frames', '10', '--rows', rows.name, '--growth', '3')
        self.assertIn(' small ', out[2])
        self.assertEqual(out[3].split(), ['#busy', '8.50', '10.50'])
        self.assertEqual(out[4].split(), ['physics', '4.00', '6.00'])  # prefix match
        self.assertEqual(out[6], '-- growth first->last (ms/f)')
        self.assertEqual([line.split()[0] for line in out[7:]], ['#busy', '#phase', '(root)'])  # ties by name

    def test_cmp_growth_without_idle_and_program(self):
        data = self._data()
        idle, program = (i for i, n in enumerate(data['nodes']) if n['callFrame']['functionName'] in ('(idle)', '(program)'))
        data['timeDeltas'] = [d * 5 if s in (data['nodes'][idle]['id'], data['nodes'][program]['id']) else d
                              for s, d in zip(data['samples'], data['timeDeltas'])]
        out = run('cmp', SMALL, self._write(data), '--frames', '10')
        growth = out[out.index('-- growth first->last (ms/f)') + 1:]
        self.assertTrue(growth)
        self.assertFalse([line for line in growth if line.split()[0] in ('#(idle)', '#(program)')])
        self.assertIn('#(program)', [line.split()[0] for line in out[:out.index('')]])  # still a row

    def test_cmp_needs_frames(self):
        with self.assertRaises(ProfileError):
            run('cmp', SMALL, SMALL)

    def test_fit(self):
        out = run('fit', '--run', '50', '10', SMALL, '--run', '100', '5', SMALL, '--run', '150', '2.5', SMALL,
                  '--ref', '100')
        self.assertTrue(out[1].startswith('ms/frame'))
        self.assertTrue(out[1].endswith('%busy@100'))
        busy = out[2].split()
        self.assertEqual(busy, ['#busy', '8.50', '17.00', '34.00', '25.50', '100.0'])

    def test_fit_needs_two_counts(self):
        with self.assertRaises(ProfileError):
            run('fit', '--run', '50', '10', SMALL)


if __name__ == '__main__':
    unittest.main()
