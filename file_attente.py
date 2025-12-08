class FileAttente:
    def __init__(self):
        self.elements = []
    
    def enfiler(self, element):
        """Ajoute un élément à la fin de la file"""
        self.elements.append(element)
    
    def defiler(self):
        """Retire et retourne l'élément au début de la file"""
        if self.est_vide():
            raise IndexError("La file est vide")
        return self.elements.pop(0)
    
    def premier(self):
        """Retourne l'élément au début sans le retirer"""
        if self.est_vide():
            raise IndexError("La file est vide")
        return self.elements[0]
    
    def est_vide(self):
        """Vérifie si la file est vide"""
        return len(self.elements) == 0
    
    def taille(self):
        """Retourne le nombre d'éléments dans la file"""
        return len(self.elements)
    
    def __str__(self):
        """Représentation en chaîne de la file"""
        return f"File: {self.elements}"


# Exemple d'utilisation
if __name__ == "__main__":
    file = FileAttente()
    
    file.enfiler("Client 1")
    file.enfiler("Client 2")
    file.enfiler("Client 3")
    
    print(file)  # File: ['Client 1', 'Client 2', 'Client 3']
    print(f"Taille: {file.taille()}")  # Taille: 3
    
    print(f"Premier: {file.premier()}")  # Premier: Client 1
    print(f"Défilé: {file.defiler()}")  # Défilé: Client 1
    print(file)  # File: ['Client 2', 'Client 3']