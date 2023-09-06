import abc,logging,os
from typing import Optional
import yaml
from localstack.utils.files import load_file,save_file
from localstack_ext.bootstrap.pods.constants import VERSION_FILE
from localstack_ext.bootstrap.pods.models import PodNode,Version
LOG=logging.getLogger(__name__)
class PodsSerializer(abc.ABC):
	@abc.abstractmethod
	def store_version(self,pod_object,path):0
	@abc.abstractmethod
	def retrieve_version(self,key,remote_path,local_path):0
class PodsSerializerYaml(PodsSerializer):
	def store_version(C,version,path):A=version;B=os.path.join(path,VERSION_FILE.format(version_no=A.version_number));save_file(B,A.to_yaml());return A.hash_ref
	def retrieve_version(E,key,remote_path,local_path):
		A=os.path.join(remote_path or local_path,key or'')
		if not os.path.isfile(A):LOG.debug('No revision file found in path %s',A);return
		D=load_file(A);B=yaml.safe_load(D);C=B.get('obj_type')
		if C==Version.__name__:return Version.from_dict(B)
		raise Exception(f"Unsupported object type '{C}'")