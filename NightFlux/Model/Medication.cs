using System.Collections.Generic;

namespace NightFlux.Model
{
    public class Medication
    {
        private List<(Ingredient, decimal)> Ingredients { get; set; }

        private Medication()
        {

        }

        public Medication WithIngredient(Ingredient ingredient)
        {
            Ingredients.Add((ingredient, 0m));
            return this;
        }

        public Medication WithMedication(Medication otherMedication)
        {
            foreach (var (ingredient, composition) in otherMedication.Ingredients)
            {
                
            }
            return this;
        }

        public static Medication Novorapid(decimal milliliters)
        {
            return new Medication()
                .WithIngredient(Ingredient.InsulinAspart(milliliters * 100))
                .WithIngredient(Ingredient.Glycerol(3.3m))
                .WithIngredient(Ingredient.DisodiumPhosphateDihydrate(0.53m))
                .WithIngredient(Ingredient.Metacresol(1.72m))
                .WithIngredient(Ingredient.Phenol(1.5m));
        }

        public static Medication Fiasp(decimal milliliters)
        {
            return new Medication()
                .WithIngredient(Ingredient.InsulinAspart(milliliters * 100))
                .WithIngredient(Ingredient.Glycerol(3.3m))
                .WithIngredient(Ingredient.DisodiumPhosphateDihydrate(0.53m))
                .WithIngredient(Ingredient.Metacresol(1.72m))
                .WithIngredient(Ingredient.Niacinamide(20.8m))
                .WithIngredient(Ingredient.LArginine(3.48m));
        }

        public static Medication GlucagenHypoKit(decimal waterAddedMilliliters)
        {
            return new Medication()
                .WithIngredient(Ingredient.WatersolubleGlucagon(100))
                .WithIngredient(Ingredient.InjectionWater(waterAddedMilliliters));
        }
    }
}
