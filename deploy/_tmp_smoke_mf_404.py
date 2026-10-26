import json,os,urllib.request,urllib.error,paramiko
HOST="169.58.236.52"; SSH_PASS=os.environ["SSH_PASS"]
remote=r"""
import json,urllib.request,urllib.error
base="http://127.0.0.1:5403/api"
env=open("/etc/qalaa-demo/sales-gateway.env",encoding="utf-8",errors="replace").read()
pw=os.environ.get('MF_PASSWORD','')
for line in env.splitlines():
  if line.startswith("MainFollowerAccounts__0__Password="):
    pw=line.split("=",1)[1]; break

def req(method,path,body=None,token=None):
  headers={"Content-Type":"application/json"}
  if token: headers["Authorization"]="Bearer "+token
  data=None if body is None else json.dumps(body).encode()
  r=urllib.request.Request(base+path,data=data,headers=headers,method=method)
  try:
    with urllib.request.urlopen(r,timeout=60) as resp:
      raw=resp.read()
      try: j=json.loads(raw.decode()) if raw else None
      except: j={"_raw_len":len(raw),"_ct":resp.headers.get("Content-Type")}
      return resp.status,j
  except urllib.error.HTTPError as e:
    b=e.read().decode("utf-8","replace")
    try: j=json.loads(b)
    except: j=b[:500]
    return e.code,j

st,mf=req("POST","/Auth/LoginMainFollower",{"userName":"mf1","password":pw})
print("LOGIN",st)
tok=mf["token"]
st,missions=req("GET","/main-follower/missions",token=tok)
print("MISSIONS",st, len(missions) if isinstance(missions,list) else missions)
if not isinstance(missions,list) or not missions:
  raise SystemExit(0)
m=missions[0]; mid=m["id"]; city=(m.get("cityValues") or m.get("CityValues") or [None])[0]
print("MISSION",mid,"CITY",city)
st,sales=req("GET",f"/main-follower/missions/{mid}/cities/{city}/sales",token=tok)
print("SALES",st, len(sales) if isinstance(sales,list) else sales)
cid=None; sid=None; name=None; phone=None
if isinstance(sales,list) and sales:
  row=sales[0]
  sid=row.get("saleId") or row.get("SaleId")
  cid=row.get("customerId") or row.get("CustomerId")
  name=row.get("customerName") or row.get("CustomerName")
  phone=row.get("customerPhone") or row.get("CustomerPhone")
  print("SAMPLE",sid,cid,name,phone)
  print("KEYS",sorted(row.keys())[:30])
if cid:
  st,prof=req("GET",f"/main-follower/missions/{mid}/cities/{city}/customers/{int(cid)}/profile",token=tok)
  print("PROFILE",st, type(prof).__name__)
  if isinstance(prof,dict):
    docs=prof.get("customerDocuments") or prof.get("CustomerDocuments") or []
    print("DOCS",len(docs), docs[0] if docs else None)
    if docs:
      did=docs[0].get("id") or docs[0].get("Id")
      st,f=req("GET",f"/main-follower/missions/{mid}/cities/{city}/customer-documents/{did}/file",token=tok)
      print("DOC_FILE",st, f if not isinstance(f,dict) or "_raw_len" in f else str(f)[:200])
if sid:
  st,docs=req("GET",f"/main-follower/missions/{mid}/cities/{city}/sales/{int(sid)}/documents",token=tok)
  print("SALE_DOCS",st, docs if not isinstance(docs,list) else len(docs))
  st,rev=req("POST","/main-follower/sale-reviews",{"missionId":mid,"cityValue":city,"saleId":int(sid),"customerName":name,"score":8,"note":"smoke"},token=tok)
  print("REVIEW8",st, rev if not isinstance(rev,dict) else {k:rev.get(k) for k in ("id","score","message","Message")})
  st,rev6=req("POST","/main-follower/sale-reviews",{"missionId":mid,"cityValue":city,"saleId":int(sid)+99999,"customerName":name,"score":6,"note":None},token=tok)
  print("REVIEW6_NO_NOTE",st, rev6)
"""
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST,username="root",password=SSH_PASS,timeout=60,allow_agent=False,look_for_keys=False)
sftp=c.open_sftp(); f=sftp.file("/tmp/smoke_mf_404.py","w"); f.write(remote); f.close(); sftp.close()
i,o,e=c.exec_command("python3 /tmp/smoke_mf_404.py",timeout=180)
print(o.read().decode()); err=e.read().decode();
print("STDERR",err[-800:] if err else "")
print("RC",o.channel.recv_exit_status()); c.close()
