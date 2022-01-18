# -*- coding: utf-8 -*-
import argparse
import logging as lg
from http.client import HTTPConnection
from urllib import response
from fastapi import FastAPI, Request
import json
from urllib.parse import urlparse
import uvicorn

app = FastAPI()
def arg_parser():
    parser = argparse.ArgumentParser("mock-rest")
    version = "%(prog)s 0.1.0"
    parser.add_argument("--version", action="version", version=version)
    parser.add_argument("--port", type=int, default=8421)
    parser.add_argument("api_links", type=str, nargs='*',)    

    return parser

args = arg_parser().parse_args()
# print(f"Args: {args}")

@app.get("/{rest_of_path:path}")
def serve_my_app(request: Request, rest_of_path: str,  status_code=200):
    # print(request.headers)
    output={"path":f"/{rest_of_path}","req_headers" : f"{request.headers}","api":{}}
    for l in args.api_links:
        try:
            url = urlparse(l if '//' in l else '//'+l)
            # print(f"Url: {url}, {url.netloc}, {url.path}")
            # Forward all headers starts with x-
            req_headers = dict({d for d in request.headers.items() if d[0].lower().startswith("x-")})
            req_headers["user-agent"] = "mock-rest/0.1.0"
            req_headers["accept-encoding"] = "gzip, deflate"
            conn = HTTPConnection(url.netloc)
            conn.request("GET", "/", headers=req_headers)
            resp = conn.getresponse()
            output["api"][l]={}
            output["api"][l]["body"] = resp.read().decode().strip()
            output["api"][l]["status"] = resp.status
            # print(resp.read().decode())
        except ConnectionError as ce:
            output["api"][l] = f"error: {ce}"
    
    print(json.dumps(output))
    api_cnt = len(output["api"])
    ok_cnt = len([av["status"] for _, av in output["api"].items() if "status" in av and av["status"] == 200])
    ok_apis = " {}/{} api returned 200/OK".format(ok_cnt,api_cnt)
    return f"Zoom!{ok_apis if api_cnt > 0 else ''}"

if __name__ == "__main__":
    # Use reload=True for development
    # uvicorn.run("main:app", host="0.0.0.0", port=args.port, reload=True, log_level="warning", workers=2)
    uvicorn.run("main:app", host="0.0.0.0", port=args.port, log_level="warning", workers=2)

