"""Short communicator smoke check. Performs random actions, never trains a policy."""
import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel

print('Waiting for HiveTraining.unity Play (no training)...', flush=True)
env = UnityEnvironment(file_name=None, seed=913, timeout_wait=55, side_channels=[StatsSideChannel()])
try:
    env.reset()
    specs = env.behavior_specs
    assert len(specs) == 1, f'Unexpected behaviors: {list(specs)}'
    name, spec = next(iter(specs.items()))
    assert name.startswith('HiveDirector'), name
    assert tuple(spec.action_spec.discrete_branches) == (6, 4, 3)
    observations = sum(int(np.prod(o.shape)) for o in spec.observation_specs)
    assert observations == 35, f'Unexpected observation count: {observations}'
    decisions = 0
    for _ in range(12):
        steps, terminal = env.get_steps(name)
        assert all(np.isfinite(a).all() for a in steps.obs)
        env.set_actions(name, spec.action_spec.random_action(len(steps)))
        decisions += len(steps)
        env.step()
    print(f'HIVE_COMMUNICATOR_PASS observations={observations} branches=6,4,3 decisions={decisions}', flush=True)
finally:
    env.close()
