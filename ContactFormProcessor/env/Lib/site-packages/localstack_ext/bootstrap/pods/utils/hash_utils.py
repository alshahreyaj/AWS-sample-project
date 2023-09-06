import hashlib,logging,os,random
from typing import Optional
from localstack_ext.bootstrap.pods.models import Revision,Version
LOG=logging.getLogger(__name__)
def random_hash():return hex(random.getrandbits(160))
def compute_file_hash(file_path,accum=None):
	B=accum;A=file_path
	try:
		with open(A,'rb')as C:
			if B:B.update(C.read())
			else:return hashlib.sha1(C.read()).hexdigest()
	except Exception as D:LOG.warning(f"Failed to open file and compute hash for file at {A}: {D}")
def compute_revision_hash(pods_node,obj_store_path):
	C='utf-8';A=pods_node
	if not A.state_files:return random_hash()
	E=map(lambda state_file:state_file.hash_ref,A.state_files);B=hashlib.sha1()
	for D in E:
		try:
			with open(os.path.join(obj_store_path,D),'rb')as F:B.update(F.read())
		except Exception as G:LOG.warning(f"Failed to open file and compute hash for {D}: {G}")
	if isinstance(A,Revision):B.update(A.rid.encode(C));B.update(str(A.revision_number).encode(C))
	elif isinstance(A,Version):B.update(str(A.version_number).encode(C))
	return B.hexdigest()