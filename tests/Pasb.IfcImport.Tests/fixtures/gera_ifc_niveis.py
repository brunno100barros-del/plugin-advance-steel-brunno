"""Gera um IFC4 sintético com 3 níveis (Térreo, 1º Pavimento, Cobertura), pilares, vigas, lajes,
uma peça repetida (IfcMappedItem), uma IfcOpeningElement e uma IfcSpace — só para testar a importação."""
import sys, uuid, string

CH = string.digits + string.ascii_uppercase + string.ascii_lowercase + '_$'
def gid():
    n = uuid.uuid4().int
    s = ''
    for _ in range(22):
        s = CH[n % 64] + s; n //= 64
    return s

L = []
def add(t):
    L.append(t); return f'#{len(L)}'

def P(x, y, z=None): return add(f"IFCCARTESIANPOINT(({x:.1f},{y:.1f}{'' if z is None else f',{z:.1f}'}))")
def D(x, y, z): return add(f"IFCDIRECTION(({x:.1f},{y:.1f},{z:.1f}))")

o = P(0, 0, 0); zA = D(0, 0, 1); xA = D(1, 0, 0)
wcs = add(f"IFCAXIS2PLACEMENT3D({o},{zA},{xA})")
ctx = add(f"IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,{wcs},$)")
body = add(f"IFCGEOMETRICREPRESENTATIONSUBCONTEXT('Body','Model',*,*,*,*,{ctx},$,.MODEL_VIEW.,$)")
mm = add("IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.)")
units = add(f"IFCUNITASSIGNMENT(({mm}))")
proj = add(f"IFCPROJECT('{gid()}',$,'Teste niveis PASB',$,$,$,$,({ctx}),{units})")
sitePl = add(f"IFCLOCALPLACEMENT($,{wcs})")
site = add(f"IFCSITE('{gid()}',$,'Terreno',$,$,{sitePl},$,$,.ELEMENT.,$,$,$,$,$)")
bldPl = add(f"IFCLOCALPLACEMENT({sitePl},{wcs})")
bld = add(f"IFCBUILDING('{gid()}',$,'Edificio teste',$,$,{bldPl},$,$,.ELEMENT.,$,$,$)")
add(f"IFCRELAGGREGATES('{gid()}',$,$,$,{proj},({site}))")
add(f"IFCRELAGGREGATES('{gid()}',$,$,$,{site},({bld}))")

def color(r, g, b, name):
    rgb = add(f"IFCCOLOURRGB($,{r},{g},{b})")
    rend = add(f"IFCSURFACESTYLERENDERING({rgb},0.,$,$,$,$,$,$,.NOTDEFINED.)")
    return add(f"IFCSURFACESTYLE('{name}',.BOTH.,({rend}))")
concrete = color(0.75, 0.75, 0.72, 'Concreto')
steel = color(0.20, 0.35, 0.60, 'Aco')

def extrusion(w, d, h, at=(0, 0, 0)):
    ppl = add(f"IFCAXIS2PLACEMENT2D({P(0,0)},$)")
    prof = add(f"IFCRECTANGLEPROFILEDEF(.AREA.,$,{ppl},{w:.1f},{d:.1f})")
    pos = add(f"IFCAXIS2PLACEMENT3D({P(*at)},$,$)")
    return add(f"IFCEXTRUDEDAREASOLID({prof},{pos},{D(0,0,1)},{h:.1f})")

def element(kind, name, storeyPl, x, y, z, solid, style, layer=None, items=None):
    if style: add(f"IFCSTYLEDITEM({solid},({style}),$)")
    rep = add(f"IFCSHAPEREPRESENTATION({body},'Body','{'MappedRepresentation' if items else 'SweptSolid'}',({items or solid}))")
    if layer: add(f"IFCPRESENTATIONLAYERASSIGNMENT('{layer}',$,({rep}),$)")
    pds = add(f"IFCPRODUCTDEFINITIONSHAPE($,$,({rep}))")
    pl = add(f"IFCLOCALPLACEMENT({storeyPl},{add(f'IFCAXIS2PLACEMENT3D({P(x,y,z)},$,$)')})")
    extra = {'IFCCOLUMN': ',.COLUMN.', 'IFCBEAM': ',.BEAM.', 'IFCSLAB': ',.FLOOR.', 'IFCSPACE': ',.ELEMENT.,.INTERNAL.,$',
             'IFCOPENINGELEMENT': ',.OPENING.', 'IFCMEMBER': ',.MULLION.'}[kind]
    return add(f"{kind}('{gid()}',$,'{name}',$,$,{pl},{pds},$" + extra + ")")

# Mullion repetido: um IfcRepresentationMap usado várias vezes
mapSolid = extrusion(50, 50, 2800)
add(f"IFCSTYLEDITEM({mapSolid},({steel}),$)")
mapRep = add(f"IFCSHAPEREPRESENTATION({body},'Body','SweptSolid',({mapSolid}))")
rmap = add(f"IFCREPRESENTATIONMAP({add(f'IFCAXIS2PLACEMENT3D({P(0,0,0)},$,$)')},{mapRep})")

levels = [('Terreo', 0.0), ('1o Pavimento', 3000.0), ('Cobertura', 6000.0)]
for name, elev in levels:
    stPl = add(f"IFCLOCALPLACEMENT({bldPl},{add(f'IFCAXIS2PLACEMENT3D({P(0,0,elev)},$,$)')})")
    st = add(f"IFCBUILDINGSTOREY('{gid()}',$,'{name}',$,$,{stPl},$,$,.ELEMENT.,{elev:.1f})")
    add(f"IFCRELAGGREGATES('{gid()}',$,$,$,{bld},({st}))")
    members = []
    if name != 'Cobertura':
        for i, (x, y) in enumerate([(0, 0), (6000, 0), (0, 5000), (6000, 5000)]):
            members.append(element('IFCCOLUMN', f'P{i+1}', stPl, x, y, 0, extrusion(300, 300, 2800), concrete, 'S-COLUMN'))
        members.append(element('IFCSLAB', 'Laje', stPl, 3000, 2500, 2800, extrusion(6300, 5300, 200), concrete, 'S-SLAB'))
        for k in range(3):
            mitem = add(f"IFCMAPPEDITEM({rmap},{add(f'IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,{P(0,0,0)},$,$)')})")
            members.append(element('IFCMEMBER', f'Montante {k+1}', stPl, 1500 + k * 1500, -200, 0, mitem, None, 'A-GLAZ', items=mitem))
        members.append(element('IFCSPACE', 'Sala', stPl, 3000, 2500, 0, extrusion(5700, 4700, 2800), None))
        members.append(element('IFCOPENINGELEMENT', 'Abertura', stPl, 3000, 0, 900, extrusion(1000, 400, 1200), None))
    else:
        for j, y in enumerate([0, 5000]):
            members.append(element('IFCBEAM', f'V{j+1}', stPl, 3000, y, 0, extrusion(6000, 150, 300), steel, 'S-BEAM'))
    if members:
        add(f"IFCRELCONTAINEDINSPATIALSTRUCTURE('{gid()}',$,$,$,({','.join(members)}),{st})")

head = ("ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(('ViewDefinition [ReferenceView]'),'2;1');\n"
        "FILE_NAME('niveis.ifc','2026-11-20T12:00:00',(''),(''),'PASB teste','PASB teste','');\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n")
with open(sys.argv[1], 'w', encoding='ascii', newline='\n') as f:
    f.write(head + ''.join(f'#{i+1}={t};\n' for i, t in enumerate(L)) + 'ENDSEC;\nEND-ISO-10303-21;\n')
print(len(L), 'entidades')
