"""Graphify pipeline helper — reads/writes graphify-out state files.

Written as a file (not an inline -c script) because Git Bash mangles inline
Python containing braces. All paths are relative to the project root.
"""
import json
import sys
from pathlib import Path

ROOT = Path('.').resolve()
OUT = ROOT / 'graphify-out'


def load_detect():
    raw = (OUT / '.graphify_detect.json').read_bytes()
    return json.loads(raw.decode('utf-8', errors='replace'))


def summarize():
    d = load_detect()
    print('total_files:', d.get('total_files'))
    print('total_words:', d.get('total_words'))
    for cat, files in d.get('files', {}).items():
        if files:
            print(cat + ':', len(files), 'files')
    sk = d.get('skipped_sensitive') or []
    print('skipped_sensitive:', len(sk), sk[:5])
    print('scan_root:', d.get('scan_root'))


def top_subdirs():
    d = load_detect()
    scan_root = d.get('scan_root', '').rstrip('/\\')
    counts = {}
    all_lists = []
    for cat in ('code', 'document', 'paper', 'image', 'video'):
        all_lists.extend(d.get('files', {}).get(cat, []))
    for f in all_lists:
        p = str(f).replace('\\', '/')
        if scan_root:
            sr = scan_root.replace('\\', '/')
            if p.startswith(sr + '/'):
                p = p[len(sr) + 1:]
        if p.startswith('graphify-out/'):
            continue
        first = p.split('/')[0] if '/' in p else '(root)'
        counts[first] = counts.get(first, 0) + 1
    top = sorted(counts.items(), key=lambda kv: -kv[1])[:5]
    for name, n in top:
        print(f'{name}: {n}')


def extract_ast():
    from graphify.extract import collect_files, extract
    d = load_detect()
    code_files = []
    for f in d.get('files', {}).get('code', []):
        p = Path(f)
        code_files.extend(collect_files(p) if p.is_dir() else [p])
    if code_files:
        result = extract(code_files, cache_root=ROOT)
        (OUT / '.graphify_ast.json').write_text(
            json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
        print('AST: %d nodes, %d edges' % (len(result['nodes']), len(result['edges'])))
    else:
        (OUT / '.graphify_ast.json').write_text(json.dumps(
            {'nodes': [], 'edges': [], 'input_tokens': 0, 'output_tokens': 0}),
            encoding='utf-8')
        print('No code files - skipping AST extraction')


def write_empty_semantic():
    (OUT / '.graphify_semantic.json').write_text(json.dumps(
        {'nodes': [], 'edges': [], 'hyperedges': [], 'input_tokens': 0, 'output_tokens': 0}),
        encoding='utf-8')
    print('Empty semantic file written (code-only fast path)')


def semantic_cache_check(spec_path):
    from graphify.cache import check_semantic_cache
    d = load_detect()
    all_files = [f for cat in ('document', 'paper', 'image')
                 for f in d['files'].get(cat, [])]
    cached_nodes, cached_edges, cached_hyperedges, uncached = check_semantic_cache(
        all_files, root=str(ROOT), prompt_file=spec_path)
    if cached_nodes or cached_edges or cached_hyperedges:
        (OUT / '.graphify_cached.json').write_text(json.dumps(
            {'nodes': cached_nodes, 'edges': cached_edges,
             'hyperedges': cached_hyperedges}, ensure_ascii=False), encoding='utf-8')
    else:
        (OUT / '.graphify_cached.json').unlink(missing_ok=True)
    (OUT / '.graphify_uncached.txt').write_text('\n'.join(uncached), encoding='utf-8')
    print('Cache: %d files hit, %d files need extraction' %
          (len(all_files) - len(uncached), len(uncached)))


def semantic_cache_save(spec_path):
    from graphify.cache import save_semantic_cache
    new_path = OUT / '.graphify_semantic_new.json'
    new = json.loads(new_path.read_text(encoding='utf-8')) if new_path.exists() else {
        'nodes': [], 'edges': [], 'hyperedges': []}
    uncached = [line for line in (OUT / '.graphify_uncached.txt').read_text(
        encoding='utf-8').splitlines() if line]
    saved = save_semantic_cache(
        new.get('nodes', []), new.get('edges', []), new.get('hyperedges', []),
        root=str(ROOT), allowed_source_files=uncached, prompt_file=spec_path)
    print('Cached %d files' % saved)


def semantic_merge():
    cached_path = OUT / '.graphify_cached.json'
    new_path = OUT / '.graphify_semantic_new.json'
    cached = json.loads(cached_path.read_text(encoding='utf-8')) if cached_path.exists() else {
        'nodes': [], 'edges': [], 'hyperedges': []}
    new = json.loads(new_path.read_text(encoding='utf-8')) if new_path.exists() else {
        'nodes': [], 'edges': [], 'hyperedges': []}
    all_nodes = cached['nodes'] + new.get('nodes', [])
    all_edges = cached['edges'] + new.get('edges', [])
    all_hyperedges = cached.get('hyperedges', []) + new.get('hyperedges', [])
    seen = set()
    deduped = []
    for n in all_nodes:
        if n['id'] not in seen:
            seen.add(n['id'])
            deduped.append(n)
    merged = {
        'nodes': deduped,
        'edges': all_edges,
        'hyperedges': all_hyperedges,
        'input_tokens': new.get('input_tokens', 0),
        'output_tokens': new.get('output_tokens', 0),
    }
    (OUT / '.graphify_semantic.json').write_text(
        json.dumps(merged, indent=2, ensure_ascii=False), encoding='utf-8')
    print('Extraction complete - %d nodes, %d edges (%d from cache, %d new)' %
          (len(deduped), len(all_edges), len(cached['nodes']), len(new.get('nodes', []))))


def merge_extract():
    ast = json.loads((OUT / '.graphify_ast.json').read_text(encoding='utf-8'))
    sem = json.loads((OUT / '.graphify_semantic.json').read_text(encoding='utf-8'))
    seen = {n['id'] for n in ast['nodes']}
    merged_nodes = list(ast['nodes'])
    for n in sem['nodes']:
        if n['id'] not in seen:
            merged_nodes.append(n)
            seen.add(n['id'])
    merged = {
        'nodes': merged_nodes,
        'edges': ast['edges'] + sem['edges'],
        'hyperedges': sem.get('hyperedges', []),
        'input_tokens': sem.get('input_tokens', 0),
        'output_tokens': sem.get('output_tokens', 0),
    }
    (OUT / '.graphify_extract.json').write_text(
        json.dumps(merged, indent=2, ensure_ascii=False), encoding='utf-8')
    print('Merged: %d nodes, %d edges (%d AST + %d semantic)' %
          (len(merged_nodes), len(merged['edges']), len(ast['nodes']), len(sem['nodes'])))


def build_graph(root_str):
    from graphify.build import build_from_json
    from graphify.cluster import cluster, score_all
    from graphify.analyze import god_nodes, surprising_connections, suggest_questions
    from graphify.report import generate
    from graphify.export import to_json
    extraction = json.loads((OUT / '.graphify_extract.json').read_text(encoding='utf-8'))
    detection = load_detect()
    G = build_from_json(extraction, root=root_str, directed=False)
    if G.number_of_nodes() == 0:
        print('ERROR: Graph is empty - extraction produced no nodes.')
        raise SystemExit(1)
    communities = cluster(G)
    cohesion = score_all(G, communities)
    tokens = {'input': extraction.get('input_tokens', 0),
              'output': extraction.get('output_tokens', 0)}
    gods = god_nodes(G)
    surprises = surprising_connections(G, communities)
    labels = {cid: 'Community ' + str(cid) for cid in communities}
    questions = suggest_questions(G, communities, labels)
    wrote = to_json(G, communities, str(OUT / 'graph.json'))
    if not wrote:
        print('ERROR: refused to shrink graphify-out/graph.json (existing graph has more nodes; #479).')
        print('If this shrink is intentional (you deleted files), re-run a full build with --force.')
        raise SystemExit(1)
    report = generate(G, communities, cohesion, labels, gods, surprises,
                      detection, tokens, root_str, suggested_questions=questions)
    (OUT / 'GRAPH_REPORT.md').write_text(report, encoding='utf-8')
    analysis = {
        'communities': {str(k): v for k, v in communities.items()},
        'cohesion': {str(k): v for k, v in cohesion.items()},
        'gods': gods,
        'surprises': surprises,
        'questions': questions,
    }
    (OUT / '.graphify_analysis.json').write_text(
        json.dumps(analysis, indent=2, ensure_ascii=False), encoding='utf-8')
    print('Graph: %d nodes, %d edges, %d communities' %
          (G.number_of_nodes(), G.number_of_edges(), len(communities)))


def health_check(root_str):
    from graphify.diagnostics import diagnose_extraction, format_diagnostic_report
    extraction = json.loads((OUT / '.graphify_extract.json').read_text(encoding='utf-8'))
    summary = diagnose_extraction(extraction, directed=False, root=root_str)
    print(format_diagnostic_report(summary))
    flags = [('%s %s' % (summary[k], label)) for k, label in (
        ('dangling_endpoint_edges', 'dangling-endpoint edges'),
        ('missing_endpoint_edges', 'missing-endpoint edges'),
        ('self_loop_edges', 'self-loop edges'),
        ('directed_same_endpoint_collapsed_edges', 'collapsed (directed) edges'),
        ('undirected_same_endpoint_collapsed_edges', 'collapsed (undirected) edges'),
    ) if summary.get(k, 0)]
    if flags:
        print('GRAPH HEALTH WARNING: ' + '; '.join(flags) +
              ' - graph may be incomplete/corrupt.')
    else:
        print('Graph health: OK (no dangling/missing/collapsed edges).')


def relabel(root_str):
    from graphify.build import build_from_json
    from graphify.cluster import score_all
    from graphify.analyze import god_nodes, surprising_connections, suggest_questions
    from graphify.report import generate
    extraction = json.loads((OUT / '.graphify_extract.json').read_text(encoding='utf-8'))
    detection = load_detect()
    analysis = json.loads((OUT / '.graphify_analysis.json').read_text(encoding='utf-8'))
    G = build_from_json(extraction, root=root_str, directed=False)
    communities = {int(k): v for k, v in analysis['communities'].items()}
    cohesion = {int(k): v for k, v in analysis['cohesion'].items()}
    tokens = {'input': extraction.get('input_tokens', 0),
              'output': extraction.get('output_tokens', 0)}
    # Community labels chosen by the host agent from node inspection
    labels = json.loads((OUT / '.graphify_labels_input.json').read_text(encoding='utf-8'))
    labels = {int(k): v for k, v in labels.items()}
    questions = suggest_questions(G, communities, labels)
    report = generate(G, communities, cohesion, labels, analysis['gods'],
                      analysis['surprises'], detection, tokens, root_str,
                      suggested_questions=questions)
    (OUT / 'GRAPH_REPORT.md').write_text(report, encoding='utf-8')
    (OUT / '.graphify_labels.json').write_text(
        json.dumps({str(k): v for k, v in labels.items()}, ensure_ascii=False),
        encoding='utf-8')
    print('Report updated with community labels')


def finalize(root_str):
    from datetime import datetime, timezone
    from graphify.detect import save_manifest
    from graphify.cli import _stamped_manifest_files
    detection = load_detect()
    extract = json.loads((OUT / '.graphify_extract.json').read_text(encoding='utf-8'))
    corpus = detection.get('all_files') or detection['files']
    manifest_files = _stamped_manifest_files(corpus, extract, ROOT)
    sem_types = ('document', 'paper', 'image')
    dispatched = {f for t, fl in detection['files'].items() if t in sem_types for f in fl}
    stamped = {f for fl in manifest_files.values() for f in fl}
    cleared = dispatched - stamped
    scan = {f for fl in corpus.values() for f in fl}
    save_manifest(manifest_files, root=root_str, scan_corpus=scan,
                  clear_semantic=cleared or None)
    input_tok = extract.get('input_tokens', 0)
    output_tok = extract.get('output_tokens', 0)
    cost_path = OUT / 'cost.json'
    if cost_path.exists():
        cost = json.loads(cost_path.read_text(encoding='utf-8'))
    else:
        cost = {'runs': [], 'total_input_tokens': 0, 'total_output_tokens': 0}
    cost['runs'].append({
        'date': datetime.now(timezone.utc).isoformat(),
        'input_tokens': input_tok,
        'output_tokens': output_tok,
        'files': detection.get('total_files', 0),
    })
    cost['total_input_tokens'] += input_tok
    cost['total_output_tokens'] += output_tok
    cost_path.write_text(json.dumps(cost, indent=2, ensure_ascii=False), encoding='utf-8')
    print('This run: %s input tokens, %s output tokens' % (format(input_tok, ','), format(output_tok, ',')))
    print('All time: %s input, %s output (%d runs)' %
          (format(cost['total_input_tokens'], ','), format(cost['total_output_tokens'], ','), len(cost['runs'])))


def cleanup():
    import os
    for name in ('.graphify_detect.json', '.graphify_extract.json',
                 '.graphify_ast.json', '.graphify_semantic.json',
                 '.graphify_analysis.json', '.needs_update',
                 '.graphify_cached.json', '.graphify_uncached.txt',
                 '.graphify_semantic_new.json', '.detect_err.log'):
        p = OUT / name
        if p.exists():
            p.unlink()
    for f in OUT.glob('.graphify_chunk_*.json'):
        f.unlink()


def dump_communities():
    analysis = json.loads((OUT / '.graphify_analysis.json').read_text(encoding='utf-8'))
    extraction = json.loads((OUT / '.graphify_extract.json').read_text(encoding='utf-8'))
    by_id = {n['id']: n.get('label', n['id']) for n in extraction['nodes']}
    for cid, members in sorted(analysis['communities'].items(), key=lambda kv: -len(kv[1])):
        print('--- Community %s (%d members) ---' % (cid, len(members)))
        for m in list(members)[:15]:
            print('   ', by_id.get(m, m))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else ''
    args = sys.argv[2:]
    if cmd == 'summarize':
        summarize()
    elif cmd == 'top-subdirs':
        top_subdirs()
    elif cmd == 'extract-ast':
        extract_ast()
    elif cmd == 'empty-semantic':
        write_empty_semantic()
    elif cmd == 'cache-check':
        semantic_cache_check(args[0])
    elif cmd == 'cache-save':
        semantic_cache_save(args[0])
    elif cmd == 'semantic-merge':
        semantic_merge()
    elif cmd == 'merge-extract':
        merge_extract()
    elif cmd == 'build':
        build_graph(args[0] if args else '.')
    elif cmd == 'health':
        health_check(args[0] if args else '.')
    elif cmd == 'relabel':
        relabel(args[0] if args else '.')
    elif cmd == 'finalize':
        finalize(args[0] if args else '.')
    elif cmd == 'cleanup':
        cleanup()
    elif cmd == 'dump-communities':
        dump_communities()
    else:
        print('Unknown command: ' + cmd)
        sys.exit(2)
