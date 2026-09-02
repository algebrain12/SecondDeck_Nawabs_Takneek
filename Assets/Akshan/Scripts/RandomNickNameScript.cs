using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RandomNickNameScript : MonoBehaviour
{
    List<String> RandomNames = new List<string>()
    {
        "Aaron", "Abigail", "Adam", "Adeline", "Adrian", "Aiden", "Alexander", "Alexis", "Alice", "Alicia",
        "Amara", "Amber", "Amelia", "Amir", "Amy", "Andrew", "Angela", "Anita", "Anna", "Anthony",
        "Apollo", "Aria", "Ariana", "Arthur", "Ash", "Asher", "Aston", "Athena", "Audrey", "Austin",
        "Ava", "Avery", "Axel", "Bailey", "Beatrice", "Bella", "Benjamin", "Bennett", "Blake", "Bradley",
        "Brandon", "Brianna", "Bridget", "Brooke", "Bruce", "Bruno", "Caleb", "Callum", "Cameron", "Camila",
        "Carlos", "Caroline", "Carter", "Cassandra", "Catherine", "Celia", "Charles", "Charlotte", "Chloe", "Christian",
        "Clara", "Cody", "Cole", "Colin", "Connor", "Cora", "Corey", "Daisy", "Damian", "Daniel",
        "Daphne", "David", "Dean", "Declan", "Delilah", "Derek", "Diana", "Dominic", "Dylan", "Elijah",
        "Elena", "Eli", "Eliana", "Eliza", "Elizabeth", "Ella", "Elliot", "Ellie", "Elsie", "Emery",
        "Emilia", "Emma", "Emmanuel", "Eric", "Ethan", "Eva", "Evan", "Evelyn", "Everett", "Ezekiel",
        "Ezra", "Faith", "Felix", "Finley", "Finn", "Fiona", "Gabriel", "Gavin", "Gemma", "Georgia",
        "Giselle", "Grace", "Graham", "Grayson", "Gregory", "Hannah", "Harper", "Harrison", "Hazel", "Henry",
        "Holden", "Holly", "Hope", "Hudson", "Hugo", "Hunter", "Ian", "Iris", "Isaac", "Isabella",
        "Isla", "Ivy", "Jack", "Jackson", "Jacob", "Jade", "James", "Jasper", "Jason", "Jaxon",
        "Jayden", "Jeremiah", "Jesse", "Jessica", "Joel", "John", "Jonah", "Jonathan", "Jordan", "Joseph",
        "Joshua", "Julia", "Julian", "Juliet", "Kai", "Kaitlyn", "Kaleb", "Katherine", "Kayla", "Leo",
        "Levi", "Liam", "Lily", "Lincoln", "Logan", "Lucas", "Lucy", "Luke", "Luna", "Mason",
        "Mateo", "Matthew", "Maya", "Mia", "Micah", "Michael", "Mila", "Miles", "Milo", "Naomi",
        "Nathan", "Nicholas", "Nora", "Oliver", "Olivia", "Owen", "Penelope", "Peter", "Phoebe", "Piper",
        "Quinn", "Rachel", "Rafael", "Riley", "River", "Robert", "Rowan", "Ruby", "Ryan", "Sadie",
        "Samuel", "Sarah", "Scarlett", "Sebastian", "Sophia", "Stella", "Theodore", "Thomas", "Tristan", "Tyler",
        "Valerie", "Victor", "Victoria", "Violet", "William", "Wyatt", "Xavier", "Zachary", "Zoe", "Zion"
    };
    public TMP_InputField inp;
    
    public void RandomName()
    {
        inp.text = RandomNames[UnityEngine.Random.Range(0,RandomNames.Count)];
    }
    void Start()
    {
        RandomName();
    }
}
