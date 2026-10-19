"""Drive this project's editor via the installed official MCP Python client."""
import argparse, asyncio, json, os
from pathlib import Path
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

async def main(args):
    async with streamablehttp_client(args.url) as (read, write, _):
        async with ClientSession(read, write) as client:
            await client.initialize()
            if args.instance:
                active = await client.call_tool('set_active_instance', {'instance': args.instance})
                if active.isError:
                    raise RuntimeError('Could not select Unity instance: ' + str(active.content))
            if args.schema:
                result = [t.model_dump(mode='json') for t in (await client.list_tools()).tools if t.name == args.schema]
            elif args.tool:
                params=json.loads(Path(args.args).read_text(encoding='utf-8-sig')) if args.args else {}
                value=await client.call_tool(args.tool, params)
                result=value.structuredContent or value.model_dump(mode='json')
            else:
                result=[t.name for t in (await client.list_tools()).tools]
            print(json.dumps(result,ensure_ascii=False,indent=2))

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--url', default=os.environ.get('HABITAT_MCP_ENDPOINT', 'http://127.0.0.1:8088/mcp'))
p.add_argument('--instance', default=os.environ.get('HABITAT_MCP_INSTANCE'), help='Exact instance ID discovered on this computer; never reuse the old workstation ID')
p.add_argument('--tool');p.add_argument('--schema');p.add_argument('--args')
args=p.parse_args()
if args.tool and not args.instance:
    p.error('--instance is required when calling a tool; discover the current Editor first')
asyncio.run(main(args))
