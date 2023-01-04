from svg.path import parse_path # !pip install svg.path
from xml.dom import minidom
import json


def get_point_at(path, distance, scale, offset):
    pos = path.point(distance)
    pos += offset
    pos *= scale
    return pos.real, pos.imag

def points_from_path(path, stepsize, scale, offset, include_last=False):
    length = path.length()
    total_distance = 0
    nspaces = int(length/stepsize) # Les points sont espacés régulièrement et comprennent le premier point (mais pas le dernier)
    if include_last:
        nsamples = nspaces + 1
    else:
        nsamples = nspaces
    if nspaces > 0:
        for i in range(nsamples):
            yield get_point_at(
                path, i/nspaces, scale, offset)

def points_from_doc(doc, nsamples=10, scale=1, offset=0, looped=[]):
    offset = offset[0] + offset[1] * 1j
    pathpoints = []
    isLoop = []
    total_length = 0
    i = 0
    for element in doc.getElementsByTagName("path"):
        for path in parse_path(element.getAttribute("d")):
            total_length += path.length()
        isLoop.append(bool(i in looped))
        i += 1
    stepsize = total_length/nsamples
    i = 0
    for element in doc.getElementsByTagName("path"):
        points = []
        for path in parse_path(element.getAttribute("d")):
            points.extend(points_from_path(
                path, stepsize, scale, offset, include_last = not isLoop[i]))
        pathpoints.append(points.copy())
        i += 1

    return pathpoints,total_length,isLoop

"""
def points_from_path(path, stepsize, scale, offset, starting_offset=0):
    length = path.length()
    total_distance = starting_offset
    while total_distance < length:
        yield get_point_at(
            path, total_distance/length, scale, offset)
        total_distance += stepsize

def points_from_doc(doc, nsamples=10, scale=1, offset=0):
    offset = offset[0] + offset[1] * 1j
    points = []
    total_length = 0
    for element in doc.getElementsByTagName("path"):
        for path in parse_path(element.getAttribute("d")):
            total_length += path.length()
    stepsize = total_length/nsamples
    starting_offset = 0 # Restes du chemin précédent
    for element in doc.getElementsByTagName("path"):
        for path in parse_path(element.getAttribute("d")):
            points.extend(points_from_path(
                path, stepsize, scale, offset, starting_offset))
            starting_offset = stepsize - (path.length()-starting_offset)%stepsize

    return points
"""

xmlstring = """<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
    <path fill="none" stroke="red"
        d="M 10,30
            A 20,20 0,0,1 50,30
            A 20,20 0,0,1 90,30
            Q 90,60 50,90
            Q 10,60 10,30 z" />
</svg>"""

shapename = input("Nom de la forme (*****.svg): ")
nsamples = int(input("Nombre de points désirés au total : "))
looped = [int(v) for v in input("Indices des chemins qui bouclent (ex '1,3') : ").split(",")]
with open(shapename+".svg","r") as file:
    xmlstring = file.read()
#print(xmlstring)
doc = minidom.parseString(xmlstring)
vb = doc.getElementsByTagName("svg")[0].getAttribute("viewBox").split(" ")
for i in range(4):
    vb[i] = float(vb[i])
dx = vb[2]-vb[0]
dy = vb[3]-vb[1]

scale = 2/max(dx,dy)
pathpoints, total_length, isLoop = points_from_doc(doc, nsamples=nsamples, scale=scale, offset=(-dx/2, -dy/2), looped=looped)
doc.unlink()


floatpathpoints = []
for i in range(len(pathpoints)):
    path = pathpoints[i]
    floatpathpoints.append([])
    for pt in path:
        floatpathpoints[i].append(pt[0])
        floatpathpoints[i].append(-pt[1])#L'axe y est dans l'autre sens
with open(f'{shapename}.json', 'w') as outfile:
    json.dump({"name":shapename,"length":total_length*scale,"points":floatpathpoints,"isLoop":isLoop}, outfile)
print("Conversion réussie !")
#print(points,len(points))
