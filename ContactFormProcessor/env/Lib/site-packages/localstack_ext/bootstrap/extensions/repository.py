import logging,os
from localstack import config,constants
from localstack.utils.venv import VirtualEnvironment
LOG=logging.getLogger(__name__)
LOCALSTACK_VENV=VirtualEnvironment(os.path.join(constants.LOCALSTACK_ROOT_FOLDER,'.venv'))
VENV_DIRECTORY='extensions/python_venv'
def get_extensions_venv():return VirtualEnvironment(os.path.join(config.dirs.var_libs,VENV_DIRECTORY))
def init():
	A=get_extensions_venv()
	if not A.exists:LOG.info('creating virtual environment at %s',A.venv_dir);A.create();LOG.info('adding localstack venv path %s',A.venv_dir);A.add_pth('localstack-venv',LOCALSTACK_VENV)
	LOG.info('injecting venv into path %s',A.venv_dir);A.inject_to_sys_path()