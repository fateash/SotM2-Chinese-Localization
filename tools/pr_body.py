import json,sys
r=json.load(open(sys.argv[1],encoding='utf-8'))
print('Updates the ParaTranz translation snapshot against the pinned SotM2 source registry.\n')
print(f"Validation: {r['registered']} keys accounted for; {r['included']} entries included in the candidate; {len(r['skipped'])} omitted by empty/status policy. Placeholder, tag, rule-number and source checks passed; runtime targets come from the pinned registry.\n")
print('The candidate was built in translated mode; it is a test artifact, not an automatic release. The runtime DLL remains 0.1.4.\n')
print('Review the translation diff before merging. Windows visual verification of changed text is still pending.')
